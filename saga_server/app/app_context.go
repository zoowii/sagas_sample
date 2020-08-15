package app

import "database/sql"

type ApplicationContext interface {
	Init() error
	Close() error
	GetDb() (*sql.DB, error)
}
