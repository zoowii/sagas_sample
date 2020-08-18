package services

import (
	"context"
	"database/sql"
	"github.com/google/uuid"
	pb "github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/app"
	"github.com/zoowii/saga_server/db"
	"log"
)

type ReplyErrorCodes = int32

const (
	Ok             ReplyErrorCodes = 0
	NotImplemented ReplyErrorCodes = 1
	ServerError    ReplyErrorCodes = 2
)

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
	defaultGlobalTxExpireSeconds = 60
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
		CreatorGroup:      nodeInfo.Group,
		CreatorService:    nodeInfo.Service,
		CreatorInstanceId: nodeInfo.InstanceId,
		ExpireSeconds:     int(expireSeconds),
		Extra:             req.Extra,
	}
	xid, err := db.CreateGlobalTx(ctx, dbConn, globalTxRecord)
	if err != nil {
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
	// TODO
	log.Println("CreateBranchTransaction")
	res = &pb.CreateBranchTransactionReply{Code: NotImplemented}
	return
}

func (s *SagaServerService) QueryGlobalTransactionDetail(ctx context.Context,
	req *pb.QueryGlobalTransactionDetailRequest) (res *pb.QueryGlobalTransactionDetailReply, err error) {
	// TODO
	log.Println("QueryGlobalTransactionDetail")
	res = &pb.QueryGlobalTransactionDetailReply{Code: NotImplemented}
	return
}

func (s *SagaServerService) SubmitGlobalTransactionState(ctx context.Context,
	req *pb.SubmitGlobalTransactionStateRequest) (res *pb.SubmitGlobalTransactionStateReply, err error) {
	// TODO
	log.Println("SubmitGlobalTransactionState")
	res = &pb.SubmitGlobalTransactionStateReply{Code: NotImplemented}
	return
}

func (s *SagaServerService) SubmitBranchTransactionState(ctx context.Context,
	req *pb.SubmitBranchTransactionStateRequest) (res *pb.SubmitBranchTransactionStateReply, err error) {
	// TODO
	log.Println("SubmitBranchTransactionState")
	res = &pb.SubmitBranchTransactionStateReply{Code: NotImplemented}
	return
}
