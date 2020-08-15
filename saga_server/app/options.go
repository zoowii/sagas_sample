package app

type Option func(ApplicationContext) error

func SetDbUrl(dbUrl string) Option {
	return func(app ApplicationContext) (err error) {
		impl, ok := app.(*applicationContextImpl)
		if !ok {
			return
		}
		impl.options.dbUrl = dbUrl
		return
	}
}
