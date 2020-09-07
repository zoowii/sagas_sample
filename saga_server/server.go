package main

import (
	"errors"
	"fmt"
	pb "github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/app"
	services "github.com/zoowii/saga_server/services"
	grpc "google.golang.org/grpc"
	"log"
	"net"
	_ "net/http/pprof"
	"os"
)

const (
	port = 9009
	network = "tcp"
)

func getDbUrl() (string, error) {
	dbUrl := os.Getenv("DATABASE_URL")
	if len(dbUrl) > 0 {
		return dbUrl, nil
	}
	return "", errors.New("DATABASE_URL not set")
}

func main() {
	address := fmt.Sprintf(":%d", port)
	listener, err := net.Listen(network, address)
	if err != nil {
		log.Fatalf("net.Listen err: %v", err)
		return
	}
	log.Println(address + " net.Listing...")
	grpcServer := grpc.NewServer()

	testDbUrl := "root:123456@tcp(127.0.0.1)/saga_server?charset=utf8&checkConnLiveness=true&parseTime=true"

	// load config from config file or environment
	dbUrl, err := getDbUrl()
	if err != nil {
		log.Printf("get db url err: %v\n", err.Error())
		log.Println("use default test db url")
		dbUrl = testDbUrl
	}

	sagaApp, err := app.NewApplicationContext(app.SetDbUrl(dbUrl))
	if err != nil {
		log.Fatalf("saga app context err: %v", err)
		return
	}
	defer sagaApp.Close()
	sagaServerService, err := services.NewSagaServerService(sagaApp)
	if err != nil {
		log.Fatalf("saga server service err: %v", err)
		return
	}
	pb.RegisterSagaServerServer(grpcServer, sagaServerService)

	// register as service to consul
	registerServer()

	if err = grpcServer.Serve(listener); err != nil {
		log.Fatalf("grpcServer.Serve err: %v", err)
	}
}
