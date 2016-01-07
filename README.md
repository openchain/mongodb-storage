# MongoDB storage provider for Openchain Server

This project implements a storage provider for Openchain Server using MongoDb as a persistence media.

It implements Records & Transactions storage and Anchors storage.

## Installation

* Edit the Openchain webapp project.json
    * Remove dnxcore5 in the frameworks section (mongodb driver currently lacks support for dnxcore) 
    * Add MongoDB storage provider as a dependency

    ```json
    {
        "version": "0.5.0-rc1",
        "entryPoint": "Openchain.Server",

        "dependencies": {
            "Openchain.Server": "0.5.0-rc1-*",
            "Openchain.Validation.PermissionBased": "0.5.0-rc1-*",
            "Openchain.Anchoring.Blockchain": "0.5.0-rc1-*",
            "Openchain.MongoDb": "0.1.0-alpha1"
        },

        "userSecretsId": "Openchain.Server",

        "commands": {
            "start": "Microsoft.AspNet.Hosting --webroot \"Webroot\" --server Microsoft.AspNet.Server.Kestrel --server.urls http://localhost:8080"
        },

        "frameworks": {
            "dnx451": {
            }
        }
    }
    ```

* Edit the Openchain webapp config.json
    * Edit the root storage section specifying the following parameters :
        * _provider_ : **MongoDb** 
        * _connection_string_ : MongoDB connection string to your MongoDb instance
        * _database_ : Name of the MongoDb database to use

    ```json
    "storage": {
        "provider": "MongoDb",
        "connection_string": "mongodb://localhost",
        "database": "openchain"
    },
    ```

    * Edit the anchoring storage section :
        * _provider_ : **MongoDb** 
        * _connection_string_ : MongoDB connection string to your MongoDb instance
        * _database_ : Name of the MongoDb database to use

    ```json
    "anchoring": {
        //...
        "storage": {
            "provider": "MongoDb",
            "connection_string": "mongodb://localhost",
            "database": "openchain"
        }
    }
    ```
    
## Running

At first startup, needed collections and indexes are created.

3 collections are used :
* _records_ containing records details
* _transactions_ containing transactions details
* _pending_transactions_ containing transactions still being committed to the database (this collection is always nearly empty).

