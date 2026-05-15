1. what we want is to read from app setting the sandbox and production midtrans apikey
2. app setting should also enable_sandbox = true/false and enable_production = true/false. This is to hide production api key to minimum access. 
3. Since the app enable staging and production endpoint. Staging is used for other child app to test payment and development. While production fires off to real snap transaction.
4. We want to also enable webhook after snap payment to registered callback url per app.
