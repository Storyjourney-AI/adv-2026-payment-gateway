1. first thing first we would create a single user called yoshua@advine.id as super admin, with password P@ssw0rd
2. we create endpoint to change password if it hasn't yet. If password is forgotten oh well we have the app and the db access anyway. Just no frontend.
3. The structure would be like this
    - User
        - Application
            - Environments (default create staging and prod per App, but can add more)
                - API Key (Auto generated)
                - Allowed Origin(s) (* for all, else specific origin(s) is allowed)
                - Webhook Url (allow empty)
                - Success Response URL
                - Failure Response URL
4. create CRUD (D soft delete) for Application and its environment
5. Comment out registration endpoint (close it)

## Frontend

1. remove registration from login page
2. remove user dashboard
3. in Admin dashboard change it to "Application" List Page
4. Have search and pagination
