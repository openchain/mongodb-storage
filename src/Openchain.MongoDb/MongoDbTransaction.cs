// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.Collections.Generic;
using System;

namespace Openchain.MongoDb
{
    public class MongoDbTransaction
    {
        [BsonId]
        public byte[] TransactionHash
        {
            get;
            set;
        }

        public byte[] MutationHash
        {
            get;
            set;
        }

        public byte[] RawData
        {
            get;
            set;
        }

        public List<byte[]> Records
        {
            get;
            set;
        }

        public BsonTimestamp Timestamp
        {
            get;
            set;
        } = new BsonTimestamp(0);

    }

    public class MongoDbPendingTransaction
    {
        [BsonId]
        public byte[] TransactionHash
        {
            get;
            set;
        }

        public byte[] MutationHash
        {
            get;
            set;
        }

        public byte[] RawData
        {
            get;
            set;
        }

        public List<MongoDbRecord> InitialRecords
        {
            get;
            set;
        }

        public BsonTimestamp Timestamp
        {
            get;
            set;
        } = new BsonTimestamp(0);

        public DateTime LockTimestamp
        {
            get;
            set;
        }

        [BsonExtraElements]
        public BsonDocument Extra { get; set; }
        public List<byte[]> AddedRecords { get; set; }

        public byte[] LockToken { get; set; }
    }

}