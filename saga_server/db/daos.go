package db

import (
	"context"
	"database/sql"
	"fmt"
	"github.com/zoowii/saga_server/api"
	"strings"
)

func CreateGlobalTx(ctx context.Context, db *sql.DB, record *GlobalTxEntity) (xid string, err error) {
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	stmt, err := tx.Prepare("insert into global_tx (xid, `state`, `end_branches`, `version`, " +
		" creator_group, creator_service," +
		" creator_instance_id, expire_seconds, extra)" +
		" values (?, ?, ?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.ExecContext(ctx, record.Xid, record.State, record.EndBranches, record.Version,
		record.CreatorGroup, record.CreatorService, record.CreatorInstanceId, record.ExpireSeconds, record.Extra)
	if err != nil {
		return
	}
	xid = record.Xid
	return
}

func CreateBranchTx(ctx context.Context, db *sql.DB, record *BranchTxEntity) (branchTxId string, err error) {
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	stmt, err := tx.PrepareContext(ctx, "insert into branch_tx (branch_tx_id, xid, `state`, `version`, " +
		" compensation_fail_times, node_group, node_service," +
		" node_instance_id, branch_service_key, branch_compensation_service_key)" +
		" values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.ExecContext(ctx, record.BranchTxId, record.Xid, record.State, record.Version,
		record.CompensationFailTimes,
		record.NodeGroup, record.NodeService, record.NodeInstanceId,
		record.BranchServiceKey, record.BranchCompensationServiceKey)
	if err != nil {
		return
	}
	branchTxId = record.BranchTxId
	return
}

const (
	globalTxTableSelectColumnsSql = "id, created_at, updated_at, xid, `state`, `version`, creator_group, creator_service, creator_instance_id, " +
		" expire_seconds, extra"
	branchTxTableSelectColumnsSql = "id, created_at, updated_at, branch_tx_id, xid, `state`, `version`, compensation_fail_times, node_group, " +
		" node_service, node_instance_id, branch_service_key, branch_compensation_service_key"
	branchTxCompensationFailLogTableSelectColumnsSql = "id, created_at, updated_at, xid, branch_tx_id, job_id, `reason`"
)

func FindGlobalTxByXidOrNull(ctx context.Context, db *sql.DB, xid string) (result *GlobalTxEntity, err error) {
	s := "select " + globalTxTableSelectColumnsSql +
		" from global_tx where xid = ? order by id asc limit 1"
	row := db.QueryRowContext(ctx, s, xid)
	entity := &GlobalTxEntity{}
	err = row.Scan(&entity.Id, &entity.CreatedAt, &entity.UpdatedAt, &entity.Xid, &entity.State, &entity.Version,
		&entity.CreatorGroup, &entity.CreatorService, &entity.CreatorInstanceId,
		&entity.ExpireSeconds, &entity.Extra)
	if err != nil && err == sql.ErrNoRows {
		err = nil
		result = nil
		return
	}
	if err != nil {
		return
	}
	result = entity
	return
}

func placeholders(n int) string {
	var b strings.Builder
	for i := 0; i < n - 1; i++ {
		b.WriteString("?,")
	}
	if n > 0 {
		b.WriteString("?")
	}
	return b.String()
}

func FindXidsOfGlobalTxsByStates(ctx context.Context, db *sql.DB,
	states []api.TxState, limit int32) (result []string, err error) {
	s := fmt.Sprintf("select xid " +
		" from global_tx where `state` in (%s) order by id desc limit ?", placeholders(len(states)))
	args := make([]interface{}, len(states)+1)
	for i, v := range states {
		args[i] = v
	}
	args[len(args)-1] = limit
	rows, err := db.QueryContext(ctx, s, args...)
	if err != nil {
		return
	}
	defer rows.Close()

	result = make([]string, 0)
	for rows.Next() {
		var item string
		err = rows.Scan(&item)
		if err != nil {
			return
		}
		result = append(result, item)
	}
	return
}

func FindAllBranchTxsByXid(ctx context.Context, db *sql.DB, xid string) (result []*BranchTxEntity, err error) {
	s := "select " + branchTxTableSelectColumnsSql +
		" from branch_tx where xid = ? order by id asc"
	rows, err := db.QueryContext(ctx, s, xid)
	if err != nil {
		return
	}
	defer rows.Close()
	for rows.Next() {
		entity := &BranchTxEntity{}
		err = rows.Scan(&entity.Id, &entity.CreatedAt, &entity.UpdatedAt, &entity.BranchTxId, &entity.Xid,
			&entity.State, &entity.Version, &entity.CompensationFailTimes,
			&entity.NodeGroup, &entity.NodeService, &entity.NodeInstanceId,
			&entity.BranchServiceKey, &entity.BranchCompensationServiceKey)
		if err != nil {
			return
		}
		result = append(result, entity)
	}
	return
}

func FindBranchTxByBranchTxId(ctx context.Context, db *sql.DB, branchTxId string) (result *BranchTxEntity, err error) {
	s := "select " + branchTxTableSelectColumnsSql +
		" from branch_tx where branch_tx_id = ? order by id asc limit 1"
	row := db.QueryRowContext(ctx, s, branchTxId)
	entity := &BranchTxEntity{}
	err = row.Scan(&entity.Id, &entity.CreatedAt, &entity.UpdatedAt, &entity.BranchTxId, &entity.Xid,
		&entity.State, &entity.Version, &entity.CompensationFailTimes,
		&entity.NodeGroup, &entity.NodeService, &entity.NodeInstanceId,
		&entity.BranchServiceKey, &entity.BranchCompensationServiceKey)
	if err != nil && err == sql.ErrNoRows {
		err = nil
		result = nil
		return
	}
	if err != nil {
		return
	}
	result = entity
	return
}

func UpdateGlobalTxState(ctx context.Context, tx *sql.Tx, xid string,
	oldVersion int32, oldState int, state int) (rowsChanged int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update global_tx set `state` = ?, `version` = `version`+1 " +
		" where xid= ? and `state`= ? and `version` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, state, xid, oldState, oldVersion)
	if err != nil {
		return
	}
	rowsChanged, err = sqlResult.RowsAffected()
	return
}

func UpdateBranchTxState(ctx context.Context, tx *sql.Tx, xid string,
	branchTxId string, oldVersion int32, oldState int, state int) (rowsChanged int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update branch_tx set `state` = ?, `version` = `version` + 1 " +
		" where  branch_tx_id = ? and xid = ? and `version` = ? and `state` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, state, branchTxId, xid, oldVersion, oldState)
	if err != nil {
		return
	}
	rowsChanged, err = sqlResult.RowsAffected()
	return
}

func UpdateBranchTxCompensationFailTimes(ctx context.Context, tx *sql.Tx,
	id uint64, oldVersion int32, failTimes int32) (rowsChanged int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update branch_tx set `compensation_fail_times` = ?, " +
		" `version` = `version` + 1 " +
		" where  id = ? and `version` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, failTimes, id, oldVersion)
	if err != nil {
		return
	}
	rowsChanged, err = sqlResult.RowsAffected()
	return
}

func UpdateBranchesStateByXid(ctx context.Context, tx *sql.Tx,
	xid string, state int) (rowsChanged int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update branch_tx set `state` = ?, `version` = `version` + 1 " +
		" where xid = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, state, xid)
	if err != nil {
		return
	}
	rowsChanged, err = sqlResult.RowsAffected()
	return
}

func FindBranchTxCompensationFailLogByJobId(ctx context.Context, tx *sql.Tx,
	jobId string) (result *BranchTxCompensationFailLogEntity, err error) {
	querySql := "select " + branchTxCompensationFailLogTableSelectColumnsSql +
		" from branch_tx_compensation_fail_log " +
		" where job_id = ?"
	row := tx.QueryRowContext(ctx, querySql, jobId)
	record := &BranchTxCompensationFailLogEntity{}
	err = row.Scan(&record.Id, &record.CreatedAt, &record.UpdatedAt,
		&record.Xid, &record.BranchTxId, &record.JobId, &record.Reason)
	if err != nil && err == sql.ErrNoRows {
		err = nil
		result = nil
		return
	}
	if err != nil {
		return
	}
	result = record
	return
}

func InsertBranchTxCompensationFailLog(ctx context.Context, tx *sql.Tx,
	xid string, branchTxId string, jobId string, reason string) (recordId uint64, err error) {
	stmt, err := tx.PrepareContext(ctx, "insert into `branch_tx_compensation_fail_log` (" +
		"xid, branch_tx_id, job_id, `reason`" +
		") values (?, ?, ?, ?)")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, xid, branchTxId, jobId, reason)
	if err != nil {
		return
	}
	lastInsertId, err := sqlResult.LastInsertId()
	if err != nil {
		return
	}
	recordId = uint64(lastInsertId)
	return
}

// 修改xid下的分支事务，把状态{oldState}的改成状态{newState}
func UpdateBranchTxsByXidFromStateToState(ctx context.Context, tx *sql.Tx,
	xid string, oldState int, newState int) (rowsAffected int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update branch_tx " +
		" set `state` = ?, `version` = `version` + 1 " +
		" where xid = ? and `state` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, newState, xid, oldState)
	if err != nil {
		return
	}
	rowsAffected, err = sqlResult.RowsAffected()
	return
}

func InsertSagaData(ctx context.Context, tx *sql.Tx,
	xid string, data []byte) (rowsAffected int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "insert into saga_data " +
		" (xid, `data`, `version`) values (?,?,?) " +
		" on duplicate key update `data`=?, `version` = `version` + 1")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, xid, data, 0)
	if err != nil {
		return
	}
	rowsAffected, err = sqlResult.RowsAffected()
	return
}

func UpdateSagaData(ctx context.Context, tx *sql.Tx,
	xid string, data []byte, oldVersion int32) (rowsAffected int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update saga_data " +
		" set `data` = ?, `version` = `version` + 1 " +
		" where `xid` = ? and `version` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, data, xid, oldVersion)
	if err != nil {
		return
	}
	rowsAffected, err = sqlResult.RowsAffected()
	return
}

func QuerySagaData(ctx context.Context, tx *sql.Tx,
	xid string) (record *SagaDataEntity, err error) {
	s := "select id, created_at, updated_at, xid, `data`, `version` from saga_data " +
		" where xid = ?"
	row := tx.QueryRowContext(ctx, s, xid)
	record = &SagaDataEntity{}
	err = row.Scan(&record.Id, &record.CreatedAt, &record.UpdatedAt,
		&record.Xid, &record.Data, &record.Version)
	if err != nil && err == sql.ErrNoRows {
		err = nil
		record = nil
		return
	}
	if err != nil {
		return
	}
	return
}
