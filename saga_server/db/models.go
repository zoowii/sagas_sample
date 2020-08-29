package db

import "time"

/**
 * 全局事务
 */
type GlobalTxEntity struct {
	Id uint64
	CreatedAt *time.Time
	UpdatedAt *time.Time
	Xid string // 全局事务ID
	State int // 全局事务的状态
	EndBranches bool // 是否不再接受新branch分支
	Version int32 // 修改版本号
	CreatorGroup string
	CreatorService string
	CreatorInstanceId string
	ExpireSeconds int
	Extra *string
}

/**
 * 分支事务
 */
type BranchTxEntity struct {
	Id uint64
	CreatedAt *time.Time
	UpdatedAt *time.Time
	BranchTxId string // 分支事务ID
	Xid string // 分支事务所属全局事务ID
	State int // 分支事务状态
	Version int32 // 修改版本号
	CompensationFailTimes int32 // 分支事务的补偿任务的失败次数
	NodeGroup string
	NodeService string
	NodeInstanceId string
	BranchServiceKey string // 分支事务的服务标识，可以找到是分支事务的主体逻辑
	BranchCompensationServiceKey string // 分支事务的补偿服务标识，可以用来找到分支事务的补偿函数
}

/**
 * 事务日志
 */
type TxLogEntity struct {
	Id uint64
	CreatedAt *time.Time
	UpdatedAt *time.Time
	Xid string
	BranchTxId string
	OperatorGroup string
	OperatorService string
	OperatorInstanceId string
	LogType string
	LogParams string
}

/**
 * 分支事务补偿失败日志
 */
type BranchTxCompensationFailLogEntity struct {
	Id uint64
	CreatedAt *time.Time
	UpdatedAt *time.Time
	Xid string
	BranchTxId string
	JobId string
	Reason string // 补偿任务失败原因
}
