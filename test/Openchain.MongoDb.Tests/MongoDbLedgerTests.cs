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

using System.Threading.Tasks;
using Openchain.Infrastructure.Tests;
using Xunit;
using System;
using MongoDB.Driver;
using Xunit;
using System.Text;
using Openchain.Infrastructure;
using System.Linq;
using System.Collections.Concurrent;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Openchain.MongoDb.Tests
{
    public class Logger : ILogger
    {

        public ITestOutputHelper Output { get; set; }

        public IDisposable BeginScopeImpl(object state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            Output.WriteLine($"[{logLevel.ToString().ToUpper().Substring(0,4)}] {formatter(state, exception)}");
        }
    }

    public class MongoDbLedgerTests : BaseLedgerTests
    {
        ITestOutputHelper Output { get; }

        MongoDbLedger store;

        public MongoDbLedgerTests(ITestOutputHelper output)
        {
            Output = output;
            var logger = new Logger() { Output = output };
                //new Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider((x, y) => y>=Microsoft.Extensions.Logging.LogLevel.Error, true).CreateLogger("Test");
            store = new MongoDbLedger(
                            new MongoDbStorageEngineConfiguration
                            {
                                ConnectionString = "mongodb://localhost",
                                Database = "openchaintest",
                                ReadLoopDelay = TimeSpan.FromMilliseconds(50),
                                ReadRetryCount = 10,
                                StaleTransactionDelay = TimeSpan.FromMinutes(10),
                                RunRollbackThread = true
                            }, logger);
            store.RecordCollection.DeleteMany(x=>true);
            store.TransactionCollection.DeleteMany(x => true);
            store.PendingTransactionCollection.DeleteMany(x => true);

            this.Engine = store;
            this.Queries = store;
            this.Indexes = store;
        }

        [Theory]
        [InlineData(4, 10, 30, 10)]
        [InlineData(8, 10, 30, 10)]
        [InlineData(16, 10, 30, 10)]
        [InlineData(40, 80, 30, 10)]
        public void ParallelismCoherency(int threadCount, int accountCount, int loopCount, int transfertStringLength)
        {
            var asset = "/asset/gold/";

            var tf = new TaskFactory();
            var tasks = Enumerable.Range(0, threadCount).Select(
                x => ParallelismCoherency(x, loopCount, accountCount, x * accountCount / transfertStringLength, transfertStringLength, asset)).ToArray();

            Task.WaitAll(tasks);

            var b=Enumerable.Range(0, accountCount).Select(x => { var t = GetAccountBalance($"/account/p{x}/", asset); t.Wait(); return t.Result; }).ToArray();
            for (int i = 0; i < accountCount; i++)
                Output.WriteLine($"/account/p{i}/ => {b[i]}");

            Output.WriteLine("Errors : ");
            foreach (var e in cb)
                Output.WriteLine("  "+e);

            var sum = tries.Sum(x => x.Value);

            Output.WriteLine("Tries : ");
            foreach (var e in tries.OrderBy(x=>x.Key))
                Output.WriteLine($"  {e.Key,3}: {e.Value,6:0} ({e.Value*1.0/sum,8:0.00%}) { new String('=', (int)(e.Value * 100.0 / sum)) }");

            Assert.Equal(threadCount*loopCount*transfertStringLength, sum);

            for (int i = 0; i < accountCount; i++)
                Assert.Equal(0, b[i]);

            Assert.True(cb.IsEmpty);

            Assert.Equal(0, store.PendingTransactionCollection.Count(x=>true));
            Assert.Equal(0, store.RecordCollection.Count(Builders<MongoDbRecord>.Filter.Exists(x => x.TransactionLock)));
        }

        ConcurrentBag<string> cb = new ConcurrentBag<string>();
        ConcurrentDictionary<int, long> tries = new ConcurrentDictionary<int, long>();
        public async Task ParallelismCoherency(int thread, int loopCount, int c, int delta, int lCount, string asset)
        {
            var r = new Random();
            for (int j = 0; j < loopCount; j++)
            {
                long amount = r.Next(1, 1000);
                for (int i = 0; i < lCount; i++)
                {
                    var tryCount = await Transfert(
                        $"/account/p{(i%lCount + delta) % c}/", $"/account/p{((i + 1)%lCount + delta) % c}/",
                        amount, asset, $"[{thread}] {j}/{loopCount} - {i}/{c}");
                    if (tryCount >0)
                    {
                        cb.Add($"[{thread}] {j}/{loopCount} - {i}/{c} /account/p{(i + delta) % c}/ => /account/p{(i + 1 + delta) % c}/ : {tryCount}");
                    }
                    tries.AddOrUpdate(tryCount, 1, (idx, co) => co + 1);
                }
            }

        }

        async Task<long> GetAccountBalance(string account, string asset)
        {
            var lAsset = LedgerPath.Parse(asset);
            var aFrom = new AccountKey(LedgerPath.Parse(account), lAsset);
            var r = await Engine.GetAccounts(new[] { aFrom });
            return r[aFrom].Balance;
        }

        ByteString LongToByteString(long amount)
        {
            var r=BitConverter.GetBytes(amount);
            Array.Reverse(r);
            return new ByteString(r);
        }

        async Task<int> Transfert(string from, string to, long amount, string asset, string prefix)
        {
            int counter = 30;
            int tryCount=0;
            int delay = 10;
            do
            {
                try
                {
                    await TransfertInternal(from, to, amount, asset);
                    counter = -1;
                }
                catch (ConcurrentMutationException e)
                {
                    Output.WriteLine($"{prefix} - {from} ==> {to} Failure {e.Data["ExceptionType"]??"ConcurrentMutation"} Retry : {tryCount}");
                    counter--;
                }
                catch (Exception e) when (e.Message.StartsWith("Lock timeout"))
                {
                    Output.WriteLine($"{prefix} - {from} ==> {to} Failure LockTimeout Retry : {tryCount}");
                    await Task.Delay(delay);
                    delay *= 2;
                    counter--;
                }
                tryCount++;
            } while (counter>0);
            return counter==-1?-tryCount:tryCount;
        }

        private async Task TransfertInternal(string from, string to, long amount, string asset)
        {
            var lAsset = LedgerPath.Parse(asset);
            var aFrom = new AccountKey(LedgerPath.Parse(from), lAsset);
            var aTo = new AccountKey(LedgerPath.Parse(to), lAsset);
            var accounts = await Engine.GetAccounts(new[] { aFrom, aTo });
            var adFrom = accounts[aFrom];
            var adTo = accounts[aTo];

            var rFrom = new Record(aFrom.Key.ToBinary(), LongToByteString(adFrom.Balance - amount), adFrom.Version);
            var rTo = new Record(aTo.Key.ToBinary(), LongToByteString(adTo.Balance + amount), adTo.Version);

            Mutation m = new Mutation(ByteString.Empty, new[] { rFrom, rTo }, ByteString.Empty);

            int c = System.Threading.Interlocked.Increment(ref gcounter);

            Transaction t = new Transaction(
                new ByteString(MessageSerializer.SerializeMutation(m)),
                DateTime.UtcNow,
                new ByteString(BitConverter.GetBytes(c))
            );

            await Engine.AddTransactions(new[] { new ByteString(MessageSerializer.SerializeTransaction(t)) });
            //Output.WriteLine($"{prefix} - {from} ==> {to} Success Retry : {tryCount}");
        }

        static int gcounter = 0;

    }
}