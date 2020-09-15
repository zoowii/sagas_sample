sagas example
==============

һ��ʹ��sagaģʽʵ�ֲַ�ʽ��������һ���Ե����ӣ����ֱ�������ṩgrpc���񣬹�ͬ����saga����

# Features

* ��ѡ�ļ���Э����saga server��֧�ֶ�̬saga��֧
* ��ѡ��Ԥ����saga��֧�߼�
* ��ҵ��ģ�������ģ���ע��saga��֧���Զ�ִ�в�������
* ����ע�������ʾ�����saga���������Ķ���
* C#, Python, Go ����ʵ�ֲ�ͬ�ӷ����demo
* ����ͨ��ʹ��grpc, ����ע��ʹ��consul, saga server�洢ʹ��mysql


# TODO

* ����`docker-compose.yml`�ļ��Ӷ����Ը���������example
* ���������Ե�����

# Notice

* ������Sagaģʽ�������ƣ���֧��ȫ�����ԭ���Ժ͸����ԣ����ǿ���֧���������ԭ���ԣ�Ҳ������ʵʱһ���ԣ�������������һ����
* ����������Ҫ����ʵ�ֱ����ԭ����
* �������ҵ�����Ͳ���������Ҫʵ���ݵ���

# Example

```
	// OrderController.cs

			using (var sagaContext = new SagaContext(_sagaCollaborator, _logger))
            {
                try
                {
                    await sagaContext.Start(form);
                    sagaContext.Bind(); // ��saga session�󶨵���ǰasync��������
                    await _createOrderSaga.createOrder(form);
                    await _createOrderSaga.reserveCustomer(form);
                    await _createOrderSaga.addLockedBalanceToMerchant(form);
                    await _createOrderSaga.approveOrder(form);
                    await _createOrderSaga.approveAddLockedBalanceToMerchant(form);
                    await _createOrderSaga.addOrderHistory(form);
                    // Ҳ���������������������ҵ���߼�

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