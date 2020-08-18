package app

import (
	"database/sql"
	"errors"
	dbModule "github.com/zoowii/saga_server/db"
)

type applicationContextImpl struct {
	ApplicationContext
	options *appContextImplOptions
	db *sql.DB
}

func NewApplicationContext(options ...Option) (app ApplicationContext, err error) {
	app = &applicationContextImpl{
		options:            &appContextImplOptions{},
	}
	for _, o := range options {
		optionErr := o(app)
		if optionErr != nil {
			err = optionErr
			return
		}
	}
	err = app.Init()
	return
}

type appContextImplOptions struct {
	dbUrl string
}

func (app *applicationContextImpl) Init() (err error)  {
	dbUrl := app.options.dbUrl
	var db *sql.DB
	if len(dbUrl) > 0 {
		db, err = dbModule.InitDb(dbUrl)
		if err != nil {
			return
		}
		app.db = db
	}
	return
}

func (app *applicationContextImpl) Close() (err error) {
	if app.db != nil {
		err = dbModule.CloseDb(app.db)
		if err != nil {
			return
		}
	}
	return
}
func (app *applicationContextImpl) GetDb() (db *sql.DB, err error) {
	if app.db != nil {
		db = app.db
	} else {
		err = errors.New("db not init yet")
	}
	return
}