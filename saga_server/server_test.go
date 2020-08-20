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

// TODO: test create branch step

// TODO: query xid

// TODO: submit global/branch state

// TODO: list global tx list