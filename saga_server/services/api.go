package services

import (
	"context"
	"database/sql"
	"fmt"
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
	NotFoundError  ReplyErrorCodes = 404
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
	// TODO
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

func (s *SagaServerService) QueryGlobalTransactionDetail(ctx context.Context,
	req *pb.QueryGlobalTransactionDetailRequest) (res *pb.QueryGlobalTransactionDetailReply, err error) {
	log.Println("QueryGlobalTransactionDetail")
	dbConn := s.dbConn
	xid := req.Xid
	globalTx, err := db.FindGlobalTxByXid(ctx, dbConn, xid)
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
		detail := &pb.TransactionBranchDetail{
			BranchId: branchTx.BranchTxId,
			Node: &pb.NodeInfo{
				Group:      branchTx.NodeGroup,
				Service:    branchTx.NodeService,
				InstanceId: branchTx.NodeInstanceId,
			},
			State:                        pb.TxState(branchTx.State),
			CompensationFailTimes:        branchTx.CompensationFailTimes,
			BranchServiceKey:             branchTx.BranchServiceKey,
			BranchCompensationServiceKey: branchTx.BranchCompensationServiceKey,
		}
		branchDetails = append(branchDetails, detail)
	}
	res = &pb.QueryGlobalTransactionDetailReply{
		Code:  Ok,
		Xid:   xid,
		State: pb.TxState(globalTx.State),
		StarterNode: &pb.NodeInfo{
			Group:      globalTx.CreatorGroup,
			Service:    globalTx.CreatorService,
			InstanceId: globalTx.CreatorInstanceId,
		},
		Branches: branchDetails,
	}
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
