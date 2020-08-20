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
	stmt, err := tx.Prepare("insert into global_tx (xid, `state`, creator_group, creator_service," +
		" creator_instance_id, expire_seconds, extra)" +
		" values (?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.Exec(record.Xid, record.State,
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
	stmt, err := tx.Prepare("insert into branch_tx (branch_tx_id, xid, `state`," +
		" compensation_fail_times, node_group, node_service," +
		" node_instance_id, branch_service_key, branch_compensation_service_key)" +
		" values (?, ?, ?, ?, ?, ?, ?, ?, ?)")
	if err != nil {
		return
	}
	_, err = stmt.Exec(record.BranchTxId, record.Xid, record.State,
		record.NodeGroup, record.NodeService, record.NodeInstanceId,
		record.BranchServiceKey, record.BranchCompensationServiceKey)
	if err != nil {
		return
	}
	branchTxId = record.BranchTxId
	return
}

func FindGlobalTxByXid(ctx context.Context, db *sql.DB, xid string) (result *GlobalTxEntity, err error) {
	s := "select id, created_at, updated_at, xid, `state`, creator_group, creator_service, creator_instance_id, " +
		" expire_seconds, extra" +
		" from global_tx where xid = ? order by id asc limit 1"
	row := db.QueryRowContext(ctx, s, xid)
	entity := &GlobalTxEntity{}
	err = row.Scan(&entity.Id, &entity.CreatedAt, &entity.UpdatedAt, &entity.Xid, &entity.State,
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
	s := "select id, created_at, updated_at, branch_tx_id, xid, `state`, compensation_fail_times, node_group, " +
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
			&entity.State, &entity.CompensationFailTimes,
			&entity.NodeGroup, &entity.NodeService, &entity.NodeInstanceId,
			&entity.BranchServiceKey, &entity.BranchCompensationServiceKey)
		if err != nil {
			return
		}
		result = append(result, entity)
	}
	return
}

// TODO
