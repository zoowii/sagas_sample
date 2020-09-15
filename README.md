sagas example
==============

一个使用saga模式实现分布式场景事务一致性的例子，多种编程语言提供grpc服务，共同构成saga事务

# Features

* 可选的集中协作者saga server，支持动态saga分支
* 可选的预定义saga分支逻辑
* 各业务模块监听本模块关注的saga分支并自动执行补偿任务
* 基于注解或者显示定义的saga补偿方法的定义
* C#, Python, Go 语言实现不同子服务的demo
* 例子通信使用grpc, 服务注册使用consul, saga server存储使用mysql


# TODO

* 增加`docker-compose.yml`文件从而可以更快启动本example
* 更多编程语言的例子

# Notice

* 受限于Saga模式本身限制，不支持全事务的原子性和隔离性，但是可以支持子事务的原子性；也不满足实时一致性，但是满足最终一致性
* 各子事务需要自行实现本身的原子性
* 子事务的业务服务和补偿服务都需要实现幂等性

# Example

```
	// OrderController.cs

			using (var sagaContext = new SagaContext(_sagaCollaborator, _logger))
            {
                try
                {
                    await sagaContext.Start(form);
                    sagaContext.Bind(); // 把saga session绑定到当前async上下文中
                    await _createOrderSaga.createOrder(form);
                    await _createOrderSaga.reserveCustomer(form);
                    await _createOrderSaga.addLockedBalanceToMerchant(form);
                    await _createOrderSaga.approveOrder(form);
                    await _createOrderSaga.approveAddLockedBalanceToMerchant(form);
                    await _createOrderSaga.addOrderHistory(form);
                    // 也可以在这里加上其他各种业务逻辑

                    await sagaContext.Commit();

                    return form.OrderId;
                }
                catch (Exception e)
                {
                    _logger.LogError("CreateOrder2 error", e);
                    await sagaContext.Rollback();
                    if (form.RejectionReason != null)
                    {
                        return form.RejectionReason.ToString();
                    }
                    return e.Message;
                }
            }
```

```
	// CreateOrderSaga.cs

	    [Compensable(nameof(cancelOrder))]
        public async Task createOrder(CreateOrderSagaData form)
        {
            await _orderService.createOrder(form);
        }

        public async Task cancelOrder(CreateOrderSagaData form)
        {
            await _orderService.cancelOrder(form);
        }
```