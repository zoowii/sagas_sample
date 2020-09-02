package services

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	pb "github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/db"
	"log"
)

/**
 * 获取分支事务branchTx所在全局事务中的其他分支事务
 */
func getBranchesOfXidExcept(ctx context.Context, dbConn *sql.DB,
	branchTx *db.BranchTxEntity) (others []*db.BranchTxEntity, err error) {
	branches, err := db.FindAllBranchTxsByXid(ctx, dbConn, branchTx.Xid)
	if err != nil {
		return
	}
	for _, b := range branches {
		if b.Id != branchTx.Id {
			others = append(others, b)
		}
	}
	return
}

/**
 * 更新某个分支事务的状态
 */
func updateBranchTxState(ctx context.Context, tx *sql.Tx,
	branchTx *db.BranchTxEntity, newState int) (rowsChanged int64, err error) {
	rowsChanged, err = db.UpdateBranchTxState(ctx, tx,
		branchTx.Xid, branchTx.BranchTxId, branchTx.Version, branchTx.State, newState)
	if err != nil {
		return
	}
	branchTx.State = newState
	branchTx.Version += 1
	return
}

func findGlobalTxOrError(ctx context.Context, dbConn *sql.DB,
	xid string) (result *db.GlobalTxEntity, err error) {
	globalTx, err := db.FindGlobalTxByXidOrNull(ctx, dbConn, xid)
	if err != nil {
		return
	}
	if globalTx == nil {
		err = errors.New(fmt.Sprintf("xid %s not found ", xid))
		return
	}
	result = globalTx
	return
}

/**
 * 提交CompensationDoing状态的全局事务状态时的回调逻辑
 */
func logicWhenSubmitGlobalTxCompensationDoing(ctx context.Context, dbConn *sql.DB, tx *sql.Tx,
	globalTx *db.GlobalTxEntity, oldState pb.TxState) (err error) {
	// 如果oldState是processing，则将processing和committed的branchTxs状态改成COMPENSATION_DOING
	if oldState != pb.TxState_PROCESSING {
		return
	}
	xid := globalTx.Xid
	_, err = db.UpdateBranchTxsByXidFromStateToState(ctx, tx, xid,
		int(pb.TxState_PROCESSING), int(pb.TxState_COMPENSATION_DOING))
	if err != nil {
		return
	}
	_, err = db.UpdateBranchTxsByXidFromStateToState(ctx, tx, xid,
		int(pb.TxState_COMMITTED), int(pb.TxState_COMPENSATION_DOING))
	if err != nil {
		return
	}
	return
}

/**
 * 提交CompensationFail状态的全局事务状态时的回调逻辑
 */
func logicWhenSubmitGlobalTxCompensationFail(ctx context.Context, dbConn *sql.DB, tx *sql.Tx,
	globalTx *db.GlobalTxEntity, oldState pb.TxState) (err error) {
	// 只有各分支事务状态都是已经CompensationFail的全局事务才能标记为CompensationFail
	xid := globalTx.Xid
	branches, err := db.FindAllBranchTxsByXid(ctx, dbConn, xid)
	if err != nil {
		return
	}
	for _, b := range branches {
		if b.State != int(pb.TxState_COMPENSATION_FAIL) {
			err = errors.New("not all branches fail, can't mark this global tx fail")
			return
		}
	}
	return
}

/**
 * 提交committed的分支事务状态时的回调逻辑
 */
func logicWhenSubmitBranchTxCommitted(ctx context.Context, dbConn *sql.DB, tx *sql.Tx,
	globalTx *db.GlobalTxEntity, branchTx *db.BranchTxEntity) (err error) {
	if !globalTx.EndBranches {
		return
	}

	// 如果这个xid的flag是EndBranches(不再接受新branch)，那么 如果这个xid的其他branches也都committed了，则这个xid要改成committed
	var otherBranches []*db.BranchTxEntity
	otherBranches, err = getBranchesOfXidExcept(ctx, dbConn, branchTx)
	if err != nil {
		return
	}
	hasNotCommitted := false
	for _, b := range otherBranches {
		if b.State != int(pb.TxState_COMMITTED) {
			hasNotCommitted = true
			break
		}
	}
	if !hasNotCommitted {
		// 这个xid的各branches都committed了
		_, err = db.UpdateGlobalTxState(ctx, tx, globalTx.Xid,
			globalTx.Version, globalTx.State, int(pb.TxState_COMMITTED))
		if err != nil {
			return
		}
		globalTx.State = int(pb.TxState_COMMITTED)
	}
	return
}

/**
 * 提交补偿失败状态的分支事务状态时的回调逻辑
 */
func logicWhenSubmitBranchTxCompensationError(ctx context.Context, dbConn *sql.DB, tx *sql.Tx,
	globalTx *db.GlobalTxEntity, branchTx *db.BranchTxEntity, jobId string, errorReason string) (err error) {
	// 如果分支事务补偿任务失败次数超过阈值，则这个branchTx要标记为补偿failed，并且xid也要标记为补偿failed
	// 为了幂等性，每次尝试补偿都要有一个不同的jobId
	log.Printf("COMPENSATION_ERROR of jobId %s", jobId)
	xid := globalTx.Xid
	branchTxId := branchTx.BranchTxId
	var rowsChanged int64
	// 补偿失败要记录日志，如果jobId没重复的话
	compensationFailLog, err := db.FindBranchTxCompensationFailLogByJobId(ctx, tx, jobId)
	if err != nil {
		return
	}
	if compensationFailLog != nil {
		// 重复jobId提交
		return
	}
	var compensationFailLogId uint64
	compensationFailLogId, err = db.InsertBranchTxCompensationFailLog(ctx, tx,
		xid, branchTxId, jobId, errorReason)
	if err != nil {
		return
	}
	if compensationFailLogId <= 0 {
		return
	}
	// 插入补偿失败日志成功，说明没有并发重复插入
	branchTx.CompensationFailTimes += 1
	rowsChanged, err = db.UpdateBranchTxCompensationFailTimes(
		ctx, tx, branchTx.Id, branchTx.Version, branchTx.CompensationFailTimes)
	if err != nil {
		return
	}
	if rowsChanged <= 0 {
		return
	}
	branchTx.Version += 1
	if branchTx.CompensationFailTimes <= defaultBranchTxCompensationMaxFailTimes {
		// 还没到允许的最大阈值
		return
	}
	rowsChanged, err = db.UpdateBranchTxState(ctx, tx, xid, branchTxId,
		branchTx.Version, branchTx.State, int(pb.TxState_COMPENSATION_FAIL))
	if err != nil {
		return
	}
	if rowsChanged <= 0 {
		return
	}
	branchTx.Version += 1
	branchTx.State = int(pb.TxState_COMPENSATION_FAIL)
	rowsChanged, err = db.UpdateGlobalTxState(ctx, tx, xid, globalTx.Version,
		globalTx.State, int(pb.TxState_COMPENSATION_FAIL))
	if err != nil {
		return
	}
	if rowsChanged <= 0 {
		return
	}
	globalTx.State = int(pb.TxState_COMPENSATION_FAIL)
	globalTx.Version += 1
	return
}

/**
 * 提交补偿完成状态的分支事务状态时的回调逻辑
 */
func logicWhenSubmitBranchTxCompensationDone(ctx context.Context, dbConn *sql.DB, tx *sql.Tx,
	globalTx *db.GlobalTxEntity, branchTx *db.BranchTxEntity) (err error) {
	// 如果这个xid的其他branches也都补偿done了，则这个xid要改成补偿done
	xid := globalTx.Xid
	var otherBranches []*db.BranchTxEntity
	otherBranches, err = getBranchesOfXidExcept(ctx, dbConn, branchTx)
	if err != nil {
		return
	}
	hasNotCompensationDone := false
	for _, b := range otherBranches {
		if b.State != int(pb.TxState_COMPENSATION_DONE) {
			hasNotCompensationDone = true
			break
		}
	}
	if !hasNotCompensationDone {
		// 这个xid的各branches都COMPENSATION_DONE了
		_, err = db.UpdateGlobalTxState(ctx, tx, xid, globalTx.Version, globalTx.State, int(pb.TxState_COMPENSATION_DONE))
		if err != nil {
			return
		}
		globalTx.State = int(pb.TxState_COMPENSATION_DONE)
	}
	return
}
