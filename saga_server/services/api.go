package services

import (
	"context"
	pb "github.com/zoowii/saga_server/api"
	"log"
)

type ReplyErrorCodes = int32

const (
	Ok             ReplyErrorCodes = 0
	NotImplemented ReplyErrorCodes = 1
)

type SagaServerService struct {
	pb.UnimplementedSagaServerServer
}

func (s *SagaServerService) CreateGlobalTransaction(ctx context.Context,
	req *pb.CreateGlobalTransactionRequest) (res *pb.CreateGlobalTransactionReply, err error) {
	// TODO
	log.Println("CreateGlobalTransaction")
	res = &pb.CreateGlobalTransactionReply{Code: NotImplemented}
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
