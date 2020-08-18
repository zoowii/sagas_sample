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


// TODO
