package db

import (
	"database/sql"
	_ "github.com/go-sql-driver/mysql"
)

func InitDb(dbUrl string) (db *sql.DB, err error) {
	db, err = sql.Open("mysql", dbUrl)
	if err != nil {
		return
	}
	return
}

func CloseDb(db *sql.DB) error {
	return db.Close()
}