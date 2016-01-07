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

using Openchain.Infrastructure;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Openchain.MongoDb
{
    public class MongoDbRecord
    {
        [BsonId]
        public byte[] Key
        {
            get;
            set;
        }

        public string KeyS
        {
            get;
            set;
        }

        public byte[] Value
        {
            get;
            set;
        }

        public byte[] Version
        {
            get;
            set;
        }

        public string[] Path
        {
            get;
            set;
        }

        public RecordType Type
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public byte[] TransactionLock { get; set; }

        [BsonExtraElements]
        public BsonDocument Extra { get; set; }

    }
}