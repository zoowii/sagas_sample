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
	Ok ReplyErrorCodes = 0
	//NotImplemented       ReplyErrorCodes = 1
	ServerError          ReplyErrorCodes = 2
	ResourceChangedError ReplyErrorCodes = 3
	NotFoundError        ReplyErrorCodes = 404
)

// TODO: branchId在创建时考虑增加上级branchId的层级关系

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
	defaultGlobalTxExpireSeconds            = 60
	defaultBranchTxCompensationMaxFailTimes = 3 // 单个branchTx允许补偿任务最大的失败次数（超过则整个全局事务标记为异常失败）
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

func branchTxToDetailInPb(branchTx *db.BranchTxEntity) *pb.TransactionBranchDetail {
	return &pb.TransactionBranchDetail{
		BranchId: branchTx.BranchTxId,
		Node: &pb.NodeInfo{
			Group:      branchTx.NodeGroup,
			Service:    branchTx.NodeService,
			InstanceId: branchTx.NodeInstanceId,
		},
		State:                        pb.TxState(branchTx.State),
		Version:                      branchTx.Version,
		CompensationFailTimes:        branchTx.CompensationFailTimes,
		BranchServiceKey:             branchTx.BranchServiceKey,
		BranchCompensationServiceKey: branchTx.BranchCompensationServiceKey,
	}
}

func (s *SagaServerService) QueryGlobalTransactionDetail(ctx context.Context,
	req *pb.QueryGlobalTransactionDetailRequest) (res *pb.QueryGlobalTransactionDetailReply, err error) {
	log.Println("QueryGlobalTransactionDetail")
	dbConn := s.dbConn
	xid := req.Xid
	globalTx, err := db.FindGlobalTxByXidOrNull(ctx, dbConn, xid)
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
		detail := branchTxToDetailInPb(branchTx)
		branchDetails = append(branchDetails, detail)
	}
	res = &pb.QueryGlobalTransactionDetailReply{
		Code:        Ok,
		Xid:         xid,
		State:       pb.TxState(globalTx.State),
		Version:     globalTx.Version,
		EndBranches: globalTx.EndBranches,
		StarterNode: &pb.NodeInfo{
			Group:      globalTx.CreatorGroup,
			Service:    globalTx.CreatorService,
			InstanceId: globalTx.CreatorInstanceId,
		},
		Branches: branchDetails,
		CreatedAt: globalTx.CreatedAt.Unix(),
		UpdatedAt: globalTx.UpdatedAt.Unix(),
		ExpireSeconds: int32(globalTx.ExpireSeconds),
	}
	return
}

func (s *SagaServerService) QueryBranchTransactionDetail(ctx context.Context,
	req *pb.QueryBranchTransactionDetailRequest) (*pb.QueryBranchTransactionDetailReply, error) {
	log.Println("QueryBranchTransactionDetail")
	dbConn := s.dbConn
	branchTxId := req.BranchId

	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.QueryBranchTransactionDetailReply, error) {
		return &pb.QueryBranchTransactionDetailReply{
			Code:  code,
			Error: msg,
		}, nil
	}

	var err error
	branchTx, err := db.FindBranchTxByBranchTxId(ctx, dbConn, branchTxId)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	xid := branchTx.Xid

	globalTx, err := db.FindGlobalTxByXidOrNull(ctx, dbConn, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if globalTx == nil {
		return sendErrorResponse(ServerError, fmt.Sprintf("branchTx's xid %s not found", xid))
	}

	detail := branchTxToDetailInPb(branchTx)

	return &pb.QueryBranchTransactionDetailReply{
		Code:          Ok,
		Xid:           xid,
		Detail:        detail,
		GlobalTxState: pb.TxState(globalTx.State),
	}, nil
}

func (s *SagaServerService) SubmitGlobalTransactionState(ctx context.Context,
	req *pb.SubmitGlobalTransactionStateRequest) (*pb.SubmitGlobalTransactionStateReply, error) {
	log.Println("SubmitGlobalTransactionState")
	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.SubmitGlobalTransactionStateReply, error) {
		return &pb.SubmitGlobalTransactionStateReply{
			Code:  code,
			Error: msg,
		}, nil
	}
	dbConn := s.dbConn
	xid := req.Xid
	state := req.State
	oldState := req.OldState
	oldVersion := req.OldVersion
	globalTx, err := db.FindGlobalTxByXidOrNull(ctx, dbConn, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if globalTx == nil {
		return sendErrorResponse(NotFoundError, fmt.Sprintf("xid %s not found", xid))
	}
	if globalTx.State != int(oldState) || globalTx.Version != oldVersion {
		return sendErrorResponse(ResourceChangedError, fmt.Sprintf("xid %s dirty change", xid))
	}
	if globalTx.State == int(state) {
		return &pb.SubmitGlobalTransactionStateReply{
			Code:  Ok,
			State: state,
		}, nil
	}
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
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
		return sendErrorResponse(ServerError, err.Error())
	}
	globalTx.State = int(state)
	if rowsChanged < 1 {
		return sendErrorResponse(ResourceChangedError, fmt.Sprintf("xid %s not change, maybe version expired", xid))
	}
	switch state {
	case pb.TxState_COMMITTED:
		{
			// 如果全局事务标记为committed，各对应分支事务还没结束的也要这么标记. 需要全局事务发起方在全局事务都committed后才把此全局事务标记为committed
			_, err = db.UpdateBranchesStateByXid(ctx, tx, xid, int(state))
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	case pb.TxState_COMPENSATION_DOING:
		{
			// 全局事务回滚
			err = logicWhenSubmitGlobalTxCompensationDoing(ctx, dbConn, tx,
				globalTx, oldState)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	case pb.TxState_COMPENSATION_FAIL:
		{
			// 标记全局事务失败了
			err = logicWhenSubmitGlobalTxCompensationFail(ctx, dbConn, tx, globalTx, oldState)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	}

	return &pb.SubmitGlobalTransactionStateReply{
		Code:  Ok,
		State: state,
	}, nil
}

func (s *SagaServerService) SubmitBranchTransactionState(ctx context.Context,
	req *pb.SubmitBranchTransactionStateRequest) (*pb.SubmitBranchTransactionStateReply, error) {
	log.Println("SubmitBranchTransactionState")
	var err error
	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.SubmitBranchTransactionStateReply, error) {
		return &pb.SubmitBranchTransactionStateReply{
			Code:  code,
			Error: msg,
		}, nil
	}
	dbConn := s.dbConn
	xid := req.Xid
	branchTxId := req.BranchId
	state := req.State
	oldState := req.OldState
	oldVersion := req.OldVersion
	jobId := req.JobId
	errorReason := req.ErrorReason
	sagaData := req.SagaData
	branchTx, err := db.FindBranchTxByBranchTxId(ctx, dbConn, branchTxId)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if branchTx == nil {
		return sendErrorResponse(NotFoundError, fmt.Sprintf("branch tx %s not found", branchTxId))
	}
	if branchTx.State != int(oldState) || branchTx.Version != oldVersion || branchTx.Xid != xid {
		return sendErrorResponse(ResourceChangedError, fmt.Sprintf("branch tx %s dirty change", branchTxId))
	}
	if branchTx.State == int(state) && state != pb.TxState_COMPENSATION_ERROR {
		// 没有改动且不是补偿失败（补偿失败可以多次）
		return &pb.SubmitBranchTransactionStateReply{
			Code:  Ok,
			State: state,
		}, nil
	}

	// 修改分支事务状态
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	rowsChanged, err := updateBranchTxState(ctx, tx, branchTx, int(state))
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if rowsChanged < 1 {
		return sendErrorResponse(ResourceChangedError,
			fmt.Sprintf("branch tx %s not change, maybe version expired", branchTxId))
	}

	var globalTx *db.GlobalTxEntity
	globalTx, err = findGlobalTxOrError(ctx, dbConn, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}

	if sagaData != nil {
		var existedSagaDataRecord *db.SagaDataEntity
		existedSagaDataRecord, err = db.QuerySagaData(ctx, tx, xid)
		if err != nil {
			return sendErrorResponse(ServerError, err.Error())
		}
		if existedSagaDataRecord == nil {
			_, err = db.InsertSagaData(ctx, tx, xid, sagaData)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		} else {
			_, err = db.UpdateSagaData(ctx, tx, xid, sagaData, existedSagaDataRecord.Version)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	}

	switch state {
	case pb.TxState_COMMITTED:
		{
			err = logicWhenSubmitBranchTxCommitted(ctx, dbConn, tx, globalTx, branchTx)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	case pb.TxState_COMPENSATION_ERROR:
		{
			err = logicWhenSubmitBranchTxCompensationError(ctx, dbConn, tx, globalTx, branchTx, jobId, errorReason)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	case pb.TxState_COMPENSATION_DONE:
		{
			err = logicWhenSubmitBranchTxCompensationDone(ctx, dbConn, tx, globalTx, branchTx)
			if err != nil {
				return sendErrorResponse(ServerError, err.Error())
			}
		}
	}

	return &pb.SubmitBranchTransactionStateReply{
		Code:  Ok,
		State: pb.TxState(branchTx.State),
	}, nil
}

func (s *SagaServerService) InitSagaData(ctx context.Context,
	req *pb.InitSagaDataRequest) (*pb.InitSagaDataReply, error) {
	log.Println("InitSagaData")
	var err error
	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.InitSagaDataReply, error) {
		return &pb.InitSagaDataReply{
			Code:  code,
			Error: msg,
		}, nil
	}
	dbConn := s.dbConn
	xid := req.Xid
	data := req.Data
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	record, err := db.QuerySagaData(ctx, tx, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if record != nil {
		log.Printf("xid %s inited saga data before, no need to init again")
		return &pb.InitSagaDataReply{
			Code: Ok,
		}, nil
	}
	_, err = db.InsertSagaData(ctx, tx, xid, data)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	record, err = db.QuerySagaData(ctx, tx, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if record == nil {
		err = errors.New("insert saga_data error")
		return sendErrorResponse(ServerError, err.Error())
	}
	return &pb.InitSagaDataReply{
		Code: Ok,
	}, nil
}

func (s *SagaServerService) GetSagaData(ctx context.Context,
	req *pb.GetSagaDataRequest) (*pb.GetSagaDataReply, error) {
	log.Println("GetSagaData")
	var err error
	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.GetSagaDataReply, error) {
		return &pb.GetSagaDataReply{
			Code:  code,
			Error: msg,
		}, nil
	}
	dbConn := s.dbConn
	xid := req.Xid
	tx, err := dbConn.BeginTx(ctx, nil)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	defer func() {
		if err != nil {
			_ = tx.Rollback()
		} else {
			err = tx.Commit()
		}
	}()
	sagaDataEntity, err := db.QuerySagaData(ctx, tx, xid)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	if sagaDataEntity == nil {
		sagaDataEntity = &db.SagaDataEntity{
			Data: make([]byte, 0),
			Version: 0,
		}
	}
	return &pb.GetSagaDataReply{
		Code: Ok,
		Data: sagaDataEntity.Data,
		Version: sagaDataEntity.Version,
	}, nil
}

func (s *SagaServerService) ListGlobalTransactionsOfStates(ctx context.Context,
	req *pb.ListGlobalTransactionsOfStatesRequest) (*pb.ListGlobalTransactionsOfStatesReply, error) {
	log.Println("ListGlobalTransactionsOfStates")
	var err error
	sendErrorResponse := func(code ReplyErrorCodes, msg string) (*pb.ListGlobalTransactionsOfStatesReply, error) {
		return &pb.ListGlobalTransactionsOfStatesReply{
			Code:  code,
			Error: msg,
		}, nil
	}
	dbConn := s.dbConn
	states := req.States
	limit := req.Limit
	if limit <= 0 {
		limit = 20
	}
	if len(states) < 1 {
		return &pb.ListGlobalTransactionsOfStatesReply{
			Code: Ok,
			Xids: make([]string, 0),
		}, nil
	}
	xids, err := db.FindXidsOfGlobalTxsByStates(ctx, dbConn, states, limit)
	if err != nil {
		return sendErrorResponse(ServerError, err.Error())
	}
	return &pb.ListGlobalTransactionsOfStatesReply{
		Code: Ok,
		Xids: xids,
	}, nil
}
