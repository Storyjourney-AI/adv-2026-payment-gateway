1. Implement Xendit Service, follow https://docs.xendit.co/docs/payment-1 
2. What we need is
    - Create API Link
    - Store as our own payment Id
    - Depending on which app creates it (which api key) the payment Id is stored under that app
    - Return the API link to caller app
    - store critical data
3. Reference jsons

Request

```
{
    "reference_id": "{{$YOUR_REFERENCE_ID}}",
    "session_type": "PAY",
    "mode": "PAYMENT_LINK",
    "amount": 150000,
    "currency": "IDR",
    "country": "ID",
    "customer": {
        "reference_id": "{{$randomUUID}}",
        "type": "INDIVIDUAL",
        "email": "customer@yourdomain.com",
        "mobile_number": "+628123456789",
        "individual_detail": {
            "given_names": "John",
            "surname": "Doe"
        }
    },
    "success_return_url": "https://yourcompany.com/order/complete",
    "cancel_return_url": "https://yourcompany.com/order/cancel"
}   
```

Response
```
{
    "payment_session_id": "ps-67527107dda8b2513acdaef0",
    "created": "2024-12-06T03:35:36.032Z",
    "updated": "2024-12-06T03:35:36.032Z",
    "status": "ACTIVE",
    "reference_id": "b767f88f-b5bc-4836-9c47-c14261909dec",
    "currency": "IDR",
    "amount": 150000,
    "country": "ID",
    "customer_id": "cust-fe8743c3-f554-4d25-a0e9-9980226c4b1b",
    "expires_at": "2024-12-06T04:05:35.049Z",
    "session_type": "PAY",
    "mode": "PAYMENT_LINK",
    "locale": "en",
    "business_id": "62440e322008e87fb29c1fd0",
    "success_return_url": "https://yourcompany.com/order/complete",
    "cancel_return_url": "https://yourcompany.com/order/cancel",
    "payment_link_url": "https://dev.xen.to/qZx5RD_7"
}

```

Webhook
```
see docs
```

Here’s a table summarizing the statuses and their descriptions for the Payment Link lifecycle:


Status

Description

Webhook event

Active

The Payment Session status will be ACTIVE immediately after creation. It remains active until it is successfully completed, expires (based on the expiry_date), or is manually canceled.

Completed

The status changes to COMPLETED when the payment or linking process (depending on the chosen flow) is successfully finished. If the user fails during the process, they can retry within the same session. During this state you will receive payment_session.completed webhook to identify you the state transition.

payment_session.completed

Expired

The status changes to EXPIRED when the expiry_date set during Payment Session creation is reached, and an expired Payment Session cannot be reactivated. During this state you will receive payment_session.expired webhook to identify you the state transition.

payment_session.expired

Canceled

If the Payment Session is manually canceled, the status changes to CANCELED immediately. The end user will no longer have access to the payment_link_url, and a canceled Payment Session cannot be reactivated.

