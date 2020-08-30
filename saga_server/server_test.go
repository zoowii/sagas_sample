package main

import (
	"context"
	"github.com/zoowii/saga_server/api"
	"google.golang.org/grpc"
	"log"
	"testing"
)

const (
	testGroup = "testGroup"
	testService = "testService"
	testInstanceId = "testInstanceId"
)

var (
	testNode = &api.NodeInfo{
		Group:      testGroup,
		Service:    testService,
		InstanceId: testInstanceId,
	}
)

func TestServerCreateGlobalTransaction(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	reply, err := client.CreateGlobalTransaction(ctx, &api.CreateGlobalTransactionRequest{
		Node:          testNode,
		ExpireSeconds: 60,
		Extra:         "test global tx",
	})
	if err != nil {
		t.Fatalf("CreateGlobalTransaction err: %v", err)
		return
	}
	log.Printf("create global tx reply: %v", reply)
}

// test create branch step
func TestServerCreateBranchTransaction(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	reply, err := client.CreateGlobalTransaction(ctx, &api.CreateGlobalTransactionRequest{
		Node:          testNode,
		ExpireSeconds: 60,
		Extra:         "test global tx",
	})
	if err != nil {
		t.Fatalf("CreateGlobalTransaction err: %v", err)
		return
	}
	log.Printf("create global tx reply: %v", reply)
	xid := reply.Xid

	createBranchTxReply, err := client.CreateBranchTransaction(ctx, &api.CreateBranchTransactionRequest{
		Node: testNode,
		Xid: xid,
		BranchServiceKey: "branch.service1.process",
		BranchCompensationServiceKey: "branch.service1.compensation",
	})
	if err != nil {
		t.Fatalf("CreateBranchTransaction err: %v", err)
		return
	}
	log.Printf("create branch tx reply: %v", createBranchTxReply)
	branchTxId := createBranchTxReply.BranchId
	log.Printf("new branch tx id: %v", branchTxId)

	// list tx branches
	globalTxDetailReply, err := client.QueryGlobalTransactionDetail(ctx, &api.QueryGlobalTransactionDetailRequest{
		Xid: xid,
	})
	if err != nil {
		t.Fatalf("QueryGlobalTransactionDetail err: %v", err)
		return
	}
	log.Printf("query global tx detail reply: %v", globalTxDetailReply)
}

// TODO: submit global state(all states)

// TODO: submit branch state(all states)

