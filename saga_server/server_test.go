package main

import (
	"context"
	"fmt"
	"github.com/google/uuid"
	"github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/services"
	"google.golang.org/grpc"
	"log"
	"testing"
)

const (
	testGroup      = "testGroup"
	testService    = "testService"
	testInstanceId = "testInstanceId"
)

var (
	testNode = &api.NodeInfo{
		Group:      testGroup,
		Service:    testService,
		InstanceId: testInstanceId,
	}
)

func generateNewJobId() string {
	u := uuid.New()
	return u.String()
}

func createTestGlobalTxOrPanic(t *testing.T, client api.SagaServerClient) (xid string) {
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
	xid = reply.Xid
	return
}

func createTestBranchTxOrPanic(t *testing.T, client api.SagaServerClient,
	xid string, branchSeq int) (branchTxId string) {
	ctx := context.Background()
	createBranchTxReply, err := client.CreateBranchTransaction(ctx, &api.CreateBranchTransactionRequest{
		Node:                         testNode,
		Xid:                          xid,
		BranchServiceKey:             fmt.Sprintf("branch.service%d.process", branchSeq),
		BranchCompensationServiceKey: fmt.Sprintf("branch.service%d.compensation", branchSeq),
	})
	if err != nil {
		t.Fatalf("CreateBranchTransaction err: %v", err)
		return
	}
	log.Printf("create branch tx reply: %v", createBranchTxReply)
	branchTxId = createBranchTxReply.BranchId
	return
}

func queryTestGlobalTxDetail(t *testing.T, client api.SagaServerClient,
	xid string) (reply *api.QueryGlobalTransactionDetailReply) {
	ctx := context.Background()
	reply, err := client.QueryGlobalTransactionDetail(ctx,
		&api.QueryGlobalTransactionDetailRequest{
			Xid: xid,
		})
	if err != nil {
		t.Fatalf("QueryGlobalTransactionDetail err: %v", err)
		return
	}
	return
}

func queryTestBranchTxDetail(t *testing.T, client api.SagaServerClient,
	branchTxId string) (reply *api.QueryBranchTransactionDetailReply) {
	ctx := context.Background()
	reply, err := client.QueryBranchTransactionDetail(ctx,
		&api.QueryBranchTransactionDetailRequest{
			BranchId: branchTxId,
		})
	if err != nil {
		t.Fatalf("QueryBranchTransactionDetail err: %v", err)
		return
	}
	return
}

func TestServerCreateGlobalTransaction(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	xid := createTestGlobalTxOrPanic(t, client)
	log.Printf("create global tx xid: %s", xid)
}

// test create branch step
func TestServerCreateBranchTransaction(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	// list tx branches
	globalTxDetailReply := queryTestGlobalTxDetail(t, client, xid)
	log.Printf("query global tx detail reply: %v", globalTxDetailReply)
}

// submit global state(all states)

func TestServerSubmitGlobalTxFailState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	globalTxDetail1 := queryTestGlobalTxDetail(t, client, xid)

	submitGlobalTxStateReply, err := client.SubmitGlobalTransactionState(ctx,
		&api.SubmitGlobalTransactionStateRequest{
			Xid:        xid,
			OldState:   globalTxDetail1.State,
			State:      api.TxState_COMPENSATION_FAIL,
			OldVersion: globalTxDetail1.Version,
		})
	if err != nil {
		log.Fatalf("SubmitGlobalTransactionState err: %v", err)
		return
	}
	log.Printf("submitGlobalTxStateReply 1: %v", submitGlobalTxStateReply)
	if submitGlobalTxStateReply.Code != services.ServerError {
		log.Fatalf("invalid reply when wrong mark global tx fail before all branches fail")
		return
	}
	// 将各分支标记为CompensationFail
	for _, b := range globalTxDetail1.Branches {
		var markBranchTxStateReply *api.SubmitBranchTransactionStateReply
		markBranchTxStateReply, err = client.SubmitBranchTransactionState(ctx,
			&api.SubmitBranchTransactionStateRequest{
				Xid:         xid,
				BranchId:    b.BranchId,
				OldState:    b.State,
				State:       api.TxState_COMPENSATION_FAIL,
				OldVersion:  b.Version,
				JobId:       generateNewJobId(),
				ErrorReason: "test",
			})
		if err != nil {
			log.Fatalf("SubmitBranchTransactionState err: %v", err)
			return
		}
		log.Printf("markBranchTxStateReply: %v", markBranchTxStateReply)
	}

	globalTxDetail2 := queryTestGlobalTxDetail(t, client, xid)

	submitGlobalTxStateReply2, err := client.SubmitGlobalTransactionState(ctx,
		&api.SubmitGlobalTransactionStateRequest{
			Xid:        xid,
			OldState:   globalTxDetail2.State,
			State:      api.TxState_COMPENSATION_FAIL,
			OldVersion: globalTxDetail2.Version,
		})
	if err != nil {
		log.Fatalf("SubmitGlobalTransactionState err: %v", err)
		return
	}
	log.Printf("submitGlobalTxStateReply 2: %v", submitGlobalTxStateReply2)

	globalTxDetailAfterSubmitState := queryTestGlobalTxDetail(t, client, xid)
	log.Printf("globalTxDetailAfterSubmitState: %v", globalTxDetailAfterSubmitState)
	if globalTxDetailAfterSubmitState.State != api.TxState_COMPENSATION_FAIL {
		log.Fatalf("TestServerSubmitGlobalTxFailState state invalid after submit")
		return
	}
	for _, b := range globalTxDetailAfterSubmitState.Branches {
		log.Printf("branch %s state %d version %d", b.BranchId, b.State, b.Version)
	}
}

func TestServerSubmitGlobalTxCompensationDoingState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	globalTxDetail1 := queryTestGlobalTxDetail(t, client, xid)

	submitGlobalTxStateReply, err := client.SubmitGlobalTransactionState(ctx,
		&api.SubmitGlobalTransactionStateRequest{
			Xid:        xid,
			OldState:   globalTxDetail1.State,
			State:      api.TxState_COMPENSATION_DOING,
			OldVersion: globalTxDetail1.Version,
		})
	if err != nil {
		log.Fatalf("SubmitGlobalTransactionState err: %v", err)
		return
	}
	log.Printf("submitGlobalTxStateReply: %v", submitGlobalTxStateReply)
	if submitGlobalTxStateReply.Code != services.Ok {
		log.Fatalf("TestServerSubmitGlobalTxCompensationDoingState error")
		return
	}
	globalTxDetailAfterSubmitState := queryTestGlobalTxDetail(t, client, xid)
	log.Printf("globalTxDetailAfterSubmitState: %v", globalTxDetailAfterSubmitState)
	if globalTxDetailAfterSubmitState.State != api.TxState_COMPENSATION_DOING {
		log.Fatalf("TestServerSubmitGlobalTxFailState state invalid after submit")
		return
	}
}

func TestServerSubmitGlobalTxCommittedState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	globalTxDetail1 := queryTestGlobalTxDetail(t, client, xid)

	submitGlobalTxStateReply, err := client.SubmitGlobalTransactionState(ctx,
		&api.SubmitGlobalTransactionStateRequest{
			Xid:        xid,
			OldState:   globalTxDetail1.State,
			State:      api.TxState_COMMITTED,
			OldVersion: globalTxDetail1.Version,
		})
	if err != nil {
		log.Fatalf("SubmitGlobalTransactionState err: %v", err)
		return
	}
	log.Printf("submitGlobalTxStateReply: %v", submitGlobalTxStateReply)
	if submitGlobalTxStateReply.Code != services.Ok {
		log.Fatalf("TestServerSubmitGlobalTxFailState error")
		return
	}
	globalTxDetailAfterSubmitState := queryTestGlobalTxDetail(t, client, xid)
	log.Printf("globalTxDetailAfterSubmitState: %v", globalTxDetailAfterSubmitState)
	if globalTxDetailAfterSubmitState.State != api.TxState_COMMITTED {
		log.Fatalf("TestServerSubmitGlobalTxFailState state invalid after submit")
		return
	}
}

// submit branch state(all states)

func TestServerSubmitBranchTxCompensationDoingState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	branchTx := queryTestBranchTxDetail(t, client, branchTxId)

	submitBranchTxStateReply, err := client.SubmitBranchTransactionState(ctx,
		&api.SubmitBranchTransactionStateRequest{
			Xid:        xid,
			BranchId:   branchTxId,
			OldState:   branchTx.Detail.State,
			State:      api.TxState_COMPENSATION_DOING,
			OldVersion: branchTx.Detail.Version,
			JobId:      generateNewJobId(),
		})
	if err != nil {
		log.Fatalf("TestServerSubmitBranchTxCompensationDoingState error")
		return
	}
	log.Printf("submitBranchTxStateReply: %v", submitBranchTxStateReply)
	branchTxAfterChange := queryTestBranchTxDetail(t, client, branchTxId)
	if branchTxAfterChange.Detail.State != api.TxState_COMPENSATION_DOING {
		log.Fatalf("TestServerSubmitBranchTxCompensationDoingState state invalid after submit")
		return
	}
}

func TestServerSubmitBranchTxCommittedState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	branchTx := queryTestBranchTxDetail(t, client, branchTxId)

	submitBranchTxStateReply, err := client.SubmitBranchTransactionState(ctx,
		&api.SubmitBranchTransactionStateRequest{
			Xid:        xid,
			BranchId:   branchTxId,
			OldState:   branchTx.Detail.State,
			State:      api.TxState_COMMITTED,
			OldVersion: branchTx.Detail.Version,
			JobId:      generateNewJobId(),
		})
	if err != nil {
		log.Fatalf("TestServerSubmitBranchTxCommittedState error")
		return
	}
	log.Printf("submitBranchTxStateReply: %v", submitBranchTxStateReply)
	branchTxAfterChange := queryTestBranchTxDetail(t, client, branchTxId)
	if branchTxAfterChange.Detail.State != api.TxState_COMMITTED {
		log.Fatalf("TestServerSubmitBranchTxCommittedState state invalid after submit")
		return
	}
}

func TestServerSubmitBranchTxCompensationErrorState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	branchTx := queryTestBranchTxDetail(t, client, branchTxId)

	submitBranchTxStateReply, err := client.SubmitBranchTransactionState(ctx,
		&api.SubmitBranchTransactionStateRequest{
			Xid:        xid,
			BranchId:   branchTxId,
			OldState:   branchTx.Detail.State,
			State:      api.TxState_COMPENSATION_ERROR,
			OldVersion: branchTx.Detail.Version,
			JobId:      generateNewJobId(),
		})
	if err != nil {
		log.Fatalf("TestServerSubmitBranchTxCompensationErrorState error")
		return
	}
	log.Printf("submitBranchTxStateReply: %v", submitBranchTxStateReply)
	branchTxAfterChange := queryTestBranchTxDetail(t, client, branchTxId)
	if branchTxAfterChange.Detail.State != api.TxState_COMPENSATION_ERROR {
		log.Fatalf("TestServerSubmitBranchTxCompensationErrorState state invalid after submit")
		return
	}
}

func TestServerSubmitBranchTxCompensationDoneState(t *testing.T) {
	cc, err := grpc.Dial(address, grpc.WithInsecure())
	if err != nil {
		t.Fatalf("grpc dial err: %v", err)
		return
	}
	client := api.NewSagaServerClient(cc)
	ctx := context.Background()
	// create global tx first
	xid := createTestGlobalTxOrPanic(t, client)

	// create a branch tx
	branchTxId := createTestBranchTxOrPanic(t, client, xid, 1)
	log.Printf("new branch tx id: %v", branchTxId)

	branchTx := queryTestBranchTxDetail(t, client, branchTxId)

	submitBranchTxStateReply, err := client.SubmitBranchTransactionState(ctx,
		&api.SubmitBranchTransactionStateRequest{
			Xid:        xid,
			BranchId:   branchTxId,
			OldState:   branchTx.Detail.State,
			State:      api.TxState_COMPENSATION_DONE,
			OldVersion: branchTx.Detail.Version,
			JobId:      generateNewJobId(),
		})
	if err != nil {
		log.Fatalf("TestServerSubmitBranchTxCompensationDoneState error")
		return
	}
	log.Printf("submitBranchTxStateReply: %v", submitBranchTxStateReply)
	branchTxAfterChange := queryTestBranchTxDetail(t, client, branchTxId)
	if branchTxAfterChange.Detail.State != api.TxState_COMPENSATION_DONE {
		log.Fatalf("TestServerSubmitBranchTxCompensationDoneState state invalid after submit")
		return
	}
}
