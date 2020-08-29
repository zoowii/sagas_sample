package db

import (
	"context"
	"database/sql"
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
	stmt, err := tx.Prepare("insert into global_tx (xid, `state`, `version`, creator_group, creator_service," +
		" creator_instance_id, expire_seconds, extra)" +
		" values (?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.ExecContext(ctx, record.Xid, record.State, record.Version,
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
		" values (?, ?, ?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.ExecContext(ctx, record.BranchTxId, record.Xid, record.State, record.Version,
		record.NodeGroup, record.NodeService, record.NodeInstanceId,
		record.BranchServiceKey, record.BranchCompensationServiceKey)
	if err != nil {
		return
	}
	branchTxId = record.BranchTxId
	return
}

func FindGlobalTxByXid(ctx context.Context, db *sql.DB, xid string) (result *GlobalTxEntity, err error) {
	s := "select id, created_at, updated_at, xid, `state`, `version`, creator_group, creator_service, creator_instance_id, " +
		" expire_seconds, extra" +
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

func FindAllBranchTxsByXid(ctx context.Context, db *sql.DB, xid string) (result []*BranchTxEntity, err error) {
	s := "select id, created_at, updated_at, branch_tx_id, xid, `state`, `version`, compensation_fail_times, node_group, " +
		" node_service, node_instance_id, branch_service_key, branch_compensation_service_key " +
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
	s := "select id, created_at, updated_at, branch_tx_id, xid, `state`, `version`, " +
		" compensation_fail_times, node_group, node_service, node_instance_id, " +
		" branch_service_key, branch_compensation_service_key" +
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
	branchId string, oldVersion int32, oldState int, state int) (rowsChanged int64, err error) {
	stmt, err := tx.PrepareContext(ctx, "update branch_tx set `state` = ?, `version` = `version` + 1 " +
		" where  branch_tx_id = ? and xid = ? and `version` = ? and `state` = ?")
	if err != nil {
		return
	}
	sqlResult, err := stmt.ExecContext(ctx, state, branchId, xid, oldVersion, oldState)
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

// TODO
