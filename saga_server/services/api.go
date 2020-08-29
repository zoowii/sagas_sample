package services

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"github.com/google/uuid"
	pb "github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/app"
	"github.com/zoowii/saga_server/db"
	"log"
)

type ReplyErrorCodes = int32

const (
	Ok                   ReplyErrorCodes = 0
	NotImplemented       ReplyErrorCodes = 1
	ServerError          ReplyErrorCodes = 2
	ResourceChangedError ReplyErrorCodes = 3
	NotFoundError        ReplyErrorCodes = 404
)

type SagaServerService struct {
	pb.UnimplementedSagaServerServer
	application app.ApplicationContext
	dbConn      *sql.DB
}

func NewSagaServerService(sagaApp app.ApplicationContext) (ss *SagaServerService, err error) {
	dbConn, err := sagaApp.GetDb()
	if err != nil {
		return
	}
	ss = &SagaServerService{
		application: sagaApp,
		dbConn:      dbConn,
	}
	return
}

func generateUniqueId() string {
	u := uuid.New()
	return u.String()
}

const (
	defaultGlobalTxExpireSeconds = 60
)

func (s *SagaServerService) CreateGlobalTransaction(ctx context.Context,
	req *pb.CreateGlobalTransactionRequest) (res *pb.CreateGlobalTransactionReply, err error) {
	log.Println("CreateGlobalTransaction")
	dbConn := s.dbConn
	nodeInfo := req.Node
	if nodeInfo == nil {
		nodeInfo = &pb.NodeInfo{}
	}
	expireSeconds := req.ExpireSeconds
	if expireSeconds <= 0 {
		expireSeconds = defaultGlobalTxExpireSeconds
	}
	globalTxRecord := &db.GlobalTxEntity{
		Xid:               generateUniqueId(),
		State:             int(pb.TxState_PROCESSING),
		Version:           0,
		CreatorGroup:      nodeInfo.Group,
		CreatorService:    nodeInfo.Service,
		CreatorInstanceId: nodeInfo.InstanceId,
		ExpireSeconds:     int(expireSeconds),
		Extra:             &req.Extra,
	}
	xid, err := db.CreateGlobalTx(ctx, dbConn, globalTxRecord)
	if err != nil {
		log.Printf("create global tx error %s\n", err.Error())
		res = &pb.CreateGlobalTransactionReply{
			Code:  ServerError,
			Error: err.Error(),
		}
		return
	}
	res = &pb.CreateGlobalTransactionReply{
		Code: Ok,
		Xid:  xid,
	}
	return
}

func (s *SagaServerService) CreateBranchTransaction(ctx context.Context,
	req *pb.CreateBranchTransactionRequest) (res *pb.CreateBranchTransactionReply, err error) {
	log.Println("CreateBranchTransaction")
	dbConn := s.dbConn
	nodeInfo := req.Node
	if nodeInfo == nil {
		nodeInfo = &pb.NodeInfo{}
	}
	xid := req.Xid
	if len(xid) < 1 {
		res = &pb.CreateBranchTransactionReply{
			Code:  ServerError,
			Error: "empty xid",
		}
		return
	}
	branchServiceKey := req.BranchServiceKey
	if len(branchServiceKey) < 1 {
		res = &pb.CreateBranchTransactionReply{
			Code:  ServerError,
			Error: "empty branchServiceKey",
		}
		return
	}
	branchCompensationServiceKey := req.BranchCompensationServiceKey
	branchTxRecord := &db.BranchTxEntity{
		BranchTxId:                   generateUniqueId(),
		Xid:                          xid,
		State:                        int(pb.TxState_PROCESSING),
		Version:                      0,
		NodeGroup:                    nodeInfo.Group,
		NodeService:                  nodeInfo.Service,
		NodeInstanceId:               nodeInfo.InstanceId,
		BranchServiceKey:             branchServiceKey,
		BranchCompensationServiceKey: branchCompensationServiceKey,
	}
	branchTxId, err := db.CreateBranchTx(ctx, dbConn, branchTxRecord)
	if err != nil {
		log.Printf("create branch tx error %s\n", err.Error())
		res = &pb.CreateBranchTransactionReply{
			Code:  ServerError,
			Error: err.Error(),
		}
		return
	}
	res = &pb.CreateBranchTransactionReply{
		Code:     Ok,
		BranchId: branchTxId,
	}
	return
}

func (s *SagaServerService) QueryGlobalTransactionDetail(ctx context.Context,
	req *pb.QueryGlobalTransactionDetailRequest) (res *pb.QueryGlobalTransactionDetailReply, err error) {
	log.Println("QueryGlobalTransactionDetail")
	dbConn := s.dbConn
	xid := req.Xid
	globalTx, err := db.FindGlobalTxByXid(ctx, dbConn, xid)
	if err != nil {
		res = &pb.QueryGlobalTransactionDetailReply{
			Code:  ServerError,
			Error: err.Error(),
		}
		return
	}
	if globalTx == nil {
		res = &pb.QueryGlobalTransactionDetailReply{
			Code:  NotFoundError,
			Error: fmt.Sprintf("xid %s not found", xid),
		}
		return
	}
	branchTxs, err := db.FindAllBranchTxsByXid(ctx, dbConn, xid)
	if err != nil {
		res = &pb.QueryGlobalTransactionDetailReply{
			Code:  ServerError,
			Error: err.Error(),
		}
		return
	}

	branchDetails := make([]*pb.TransactionBranchDetail, 0)
	for _, branchTx := range branchTxs {
		detail := &pb.TransactionBranchDetail{
			BranchId: branchTx.BranchTxId,
			Node: &pb.NodeInfo{
				Group:      branchTx.NodeGroup,
				Service:    branchTx.NodeService,
				InstanceId: branchTx.NodeInstanceId,
			},
			State:                        pb.TxState(branchTx.State),
			CompensationFailTimes:        branchTx.CompensationFailTimes,
			BranchServiceKey:             branchTx.BranchServiceKey,
			BranchCompensationServiceKey: branchTx.BranchCompensationServiceKey,
		}
		branchDetails = append(branchDetails, detail)
	}
	res = &pb.QueryGlobalTransactionDetailReply{
		Code:  Ok,
		Xid:   xid,
		State: pb.TxState(globalTx.State),
		StarterNode: &pb.NodeInfo{
			Group:      globalTx.CreatorGroup,
			Service:    globalTx.CreatorService,
			InstanceId: globalTx.CreatorInstanceId,
		},
		Branches: branchDetails,
	}
	return
}

func (s *SagaServerService) SubmitGlobalTransactionState(ctx context.Context,
	req *pb.SubmitGlobalTransactionStateRequest) (res *pb.SubmitGlobalTransactionStateReply, err error) {
	log.Println("SubmitGlobalTransactionState")
	sendErrorResponse := func(code ReplyErrorCodes, msg string) {
		res = &pb.SubmitGlobalTransactionStateReply{
			Code:  code,
			Error: msg,
		}
	}
	dbConn := s.dbConn
	xid := req.Xid
	state := req.State
	oldState := req.OldState
	oldVersion := req.OldVersion
	globalTx, err := db.FindGlobalTxByXid(ctx, dbConn, xid)
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	if globalTx == nil {
		sendErrorResponse(NotFoundError, fmt.Sprintf("xid %s not found", xid))
		return
	}
	if globalTx.State != int(oldState) || globalTx.Version != oldVersion {
		sendErrorResponse(ResourceChangedError, fmt.Sprintf("xid %s dirty change", xid))
		return
	}
	if globalTx.State == int(state) {
		res = &pb.SubmitGlobalTransactionStateReply{
			Code:  Ok,
			State: state,
		}
		return
	}
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	rowsChanged, err := db.UpdateGlobalTxState(ctx, tx, xid, oldVersion, globalTx.State, int(state))
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	globalTx.State = int(state)
	if rowsChanged < 1 {
		sendErrorResponse(ResourceChangedError, fmt.Sprintf("xid %s not change, maybe version expired", xid))
		return
	}
	if state == pb.TxState_COMMITTED {
		// 如果全局事务标记为committed，各对应分支事务还没结束的也要这么标记
		_, err = db.UpdateBranchesStateByXid(ctx, tx, xid, int(state))
		if err != nil {
			sendErrorResponse(ServerError, err.Error())
			return
		}
	}

	res = &pb.SubmitGlobalTransactionStateReply{
		Code:  Ok,
		State: state,
	}
	return
}

func (s *SagaServerService) SubmitBranchTransactionState(ctx context.Context,
	req *pb.SubmitBranchTransactionStateRequest) (res *pb.SubmitBranchTransactionStateReply, err error) {
	log.Println("SubmitBranchTransactionState")
	sendErrorResponse := func(code ReplyErrorCodes, msg string) {
		res = &pb.SubmitBranchTransactionStateReply{
			Code:  code,
			Error: msg,
		}
	}
	dbConn := s.dbConn
	xid := req.Xid
	branchTxId := req.BranchId
	state := req.State
	oldState := req.OldState
	oldVersion := req.OldVersion
	jobId := req.JobId
	branchTx, err := db.FindBranchTxByBranchTxId(ctx, dbConn, branchTxId)
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	if branchTx == nil {
		sendErrorResponse(NotFoundError, fmt.Sprintf("branch tx %s not found", branchTxId))
		return
	}
	if branchTx.State != int(oldState) || branchTx.Version != oldVersion || branchTx.Xid != xid {
		sendErrorResponse(ResourceChangedError, fmt.Sprintf("branch tx %s dirty change", branchTxId))
		return
	}
	if branchTx.State == int(state) {
		// 没有改动
		res = &pb.SubmitBranchTransactionStateReply{
			Code:  Ok,
			State: state,
		}
		return
	}

	// 修改分支事务状态
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	rowsChanged, err := db.UpdateBranchTxState(ctx, tx, xid, branchTxId, oldVersion, branchTx.State, int(state))
	if err != nil {
		sendErrorResponse(ServerError, err.Error())
		return
	}
	branchTx.State = int(state)
	if rowsChanged < 1 {
		sendErrorResponse(ResourceChangedError,
			fmt.Sprintf("branch tx %s not change, maybe version expired", branchTxId))
		return
	}

	getOtherBranches := func() (others []*db.BranchTxEntity, err error) {
		branches, err := db.FindAllBranchTxsByXid(ctx, dbConn, xid)
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

	if state == pb.TxState_COMMITTED {
		// 如果这个xid的其他branches也都committed了，则这个xid要改成committed
		var otherBranches []*db.BranchTxEntity
		otherBranches, err = getOtherBranches()
		if err != nil {
			sendErrorResponse(ServerError, err.Error())
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
			var globalTx *db.GlobalTxEntity
			globalTx, err = db.FindGlobalTxByXid(ctx, dbConn, xid)
			if err != nil {
				sendErrorResponse(ServerError, err.Error())
				return
			}
			if globalTx == nil {
				err = errors.New(fmt.Sprintf("xid %s not found ", xid))
				sendErrorResponse(ResourceChangedError, err.Error())
				return
			}
			_, err = db.UpdateGlobalTxState(ctx, tx, xid, globalTx.Version, globalTx.State, int(pb.TxState_COMMITTED))
			if err != nil {
				sendErrorResponse(ServerError, err.Error())
				return
			}
			globalTx.State = int(pb.TxState_COMMITTED)
		}
	} else if state == pb.TxState_COMPENSATION_ERROR {
		// TODO: 如果分支事务补偿任务失败次数超过阈值，则这个branchTx要标记为补偿failed，并且xid也要标记为补偿failed
		// TODO: 为了幂等性，每次尝试补偿都要有一个不同的jobId
		log.Printf("COMPENSATION_ERROR of jobId %s", jobId)
		// TODO: 数据库记录下每次补偿失败的记录，从而知道branch的补偿失败的次数和各jobId
	} else if state == pb.TxState_COMPENSATION_DONE {
		// 如果这个xid的其他branches也都补偿done了，则这个xid要改成补偿done
		var otherBranches []*db.BranchTxEntity
		otherBranches, err = getOtherBranches()
		if err != nil {
			sendErrorResponse(ServerError, err.Error())
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
			var globalTx *db.GlobalTxEntity
			globalTx, err = db.FindGlobalTxByXid(ctx, dbConn, xid)
			if err != nil {
				sendErrorResponse(ServerError, err.Error())
				return
			}
			if globalTx == nil {
				err = errors.New(fmt.Sprintf("xid %s not found ", xid))
				sendErrorResponse(ResourceChangedError, err.Error())
				return
			}
			_, err = db.UpdateGlobalTxState(ctx, tx, xid, globalTx.Version, globalTx.State, int(pb.TxState_COMPENSATION_DONE))
			if err != nil {
				sendErrorResponse(ServerError, err.Error())
				return
			}
			globalTx.State = int(pb.TxState_COMPENSATION_DONE)
		}
	}

	res = &pb.SubmitBranchTransactionStateReply{
		Code:  Ok,
		State: pb.TxState(branchTx.State),
	}
	return
}
