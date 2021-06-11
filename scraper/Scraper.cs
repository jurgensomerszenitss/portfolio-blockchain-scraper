using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using RomeScraper.Config;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Newtonsoft.Json;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using System.IO;
using Nethereum.StandardTokenEIP20.Events.DTO;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.StandardTokenEIP20;
using System.Reflection;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace RomeScraper.Scraper
{
    public class Scraper : IScraper
    {
        public Scraper(ILogger logger, EthSettings ethSettings)
        {
            _logger = logger;
            _ethSettings = ethSettings;
        }

        private readonly ILogger _logger;
        private readonly EthSettings _ethSettings;


        private string LoadAbi()
        {
            var file = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/contracts/uniswap.abi";
            _logger.LogInformation(file);
            if (File.Exists(file))
                return File.ReadAllText(file);

            throw new FileNotFoundException("No ABI file found");
        }

        private void PrintTx(Transaction tx)
        {
            _logger.LogInformation($"Tx From  : {tx.From}");
            _logger.LogInformation($"Tx To    : {tx.To}");
            _logger.LogInformation($"Tx Block : {tx.BlockHash} / {tx.BlockNumber}");
            _logger.LogInformation($"Tx Index : {tx.TransactionIndex}");
            _logger.LogInformation($"Tx Hash  : {tx.TransactionHash}");
            _logger.LogInformation("-".PadLeft(30, '-'));
        }

        private void PrintTransfer(TransferEventDTO dto)
        {
            _logger.LogInformation($"Log From  : {dto.From}");
            _logger.LogInformation($"Log To    : {dto.To}");
            _logger.LogInformation($"Log Value : {dto.Value}");
        }


        private string ConvertAddressToTopic(string address)
        {
            var result = address;
            if (address.StartsWith("0x"))
                result = result.Substring(2);

            return "0x" + result.PadLeft(64, '0');
        }

        public async Task StartAsync()
        {
            using (var client = new WebSocketClient(_ethSettings.Node))
            {
                try
                {
                    var web3 = new Web3(client);

                    var blockNumber = 11739004L;
                    var currentBlock = new BlockParameter(new HexBigInteger(blockNumber));
                    var lastBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    var lastBlock = lastBlockNumber.Value;
                    var range = lastBlock - blockNumber;
                    var index = 0;
                    while (blockNumber <= lastBlock)
                    {
                        var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(currentBlock);
                        SaveIndexes(block.Transactions);
                        blockNumber++;
                        currentBlock.SetValue(blockNumber);// = new BlockParameter(new HexBigInteger(blockNumber));
                        index ++;
                        _logger.LogInformation($"{index} of {range}");
                    }
                }
                catch (Exception exc)
                {
                    throw;
                }
            }
        }

        private void SaveIndexes(Transaction[] transactions)
        {
            // for new, save to a csv file
            var indexes = transactions.SelectMany(x => GetIndexes(x)).Select(x => x.ToCsv());

            File.AppendAllLines("blockchain.csv", indexes);
        }

        private IList<TransactionIndex> GetIndexes(Transaction transaction)
        {
            var fromIndex = new TransactionIndex { TxHash = transaction.From, BlockNumber = transaction.BlockNumber.HexValue, TxIndex = transaction.TransactionIndex.HexValue};
            var toIndex = new TransactionIndex { TxHash = transaction.To, BlockNumber = transaction.BlockNumber.HexValue, TxIndex = transaction.TransactionIndex.HexValue};

            return new [] { fromIndex, toIndex};
        }

        public class TransactionIndex
        {
            public string TxHash { get; set; }
            public string BlockNumber { get; set; }
            public string TxIndex { get; set; }

            public string ToCsv()
            {
                return $"{TxHash},{BlockNumber},{TxIndex}";
            }
        }

        public async Task TestContract()
        {
            var contractAddress = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            //var abi = LoadAbi();
            using (var client = new WebSocketClient(_ethSettings.Node))
            {
                try
                {
                    var web3 = new Web3(client);
                    //Nethereum.JsonRpc.Client.ClientBase.ConnectionTimeout = TimeSpan.FromMinutes(2);

                    // var standardTokenService = new StandardTokenService(web3, contractAddress);
                    // var transfersEvent = standardTokenService.GetTransferEvent();
                    var abi = LoadAbi();
                    var contract = web3.Eth.GetContract(abi, contractAddress);
                    var transfersEvent = contract.GetEvent("Transfer");

                    var lastBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                    //  var filterInput = transfersEvent.CreateFilterInput(new BlockParameter(11738674), BlockParameter.CreateLatest());
                    var filter = transfersEvent.CreateFilterInput(new BlockParameter(11738674), new BlockParameter(lastBlockNumber));
                    //var filter = await transfersEvent.CreateFilterAsync(new BlockParameter(new HexBigInteger(11739004)));
                    var tokenLogs = await transfersEvent.GetAllChanges<TransferEventDTO>(filter);

                    // var filterInput = transfersEvent.CreateFilterInput(null, new string[] { ConvertAddressToTopic("0x6d10c8964856a3686312884232b6b3cf25caea1d")}, new BlockParameter(new HexBigInteger(11739004)), BlockParameter.CreateLatest());
                    // var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
                    // _logger.LogInformation($"Token logs found :{logs.Length}");

                    // var tokenLogs = Event<TransferEventDTO>.DecodeAllEvents<TransferEventDTO>(logs);

                    _logger.LogInformation($"Event logs found :{tokenLogs.Count}");
                    foreach (var log in tokenLogs)
                    {
                        PrintTransfer(log.Event);
                    }
                }
                catch (Exception exc)
                {
                    throw;
                }

            }
        }

        public async Task GetBlock()
        {
            using (var client = new WebSocketClient(_ethSettings.Node))
            {
                var web3 = new Web3(client);
                var lastBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(lastBlockNumber);
                foreach (var tx in block.Transactions)
                {
                    PrintTx(tx);
                }
            }
        }

        public async Task TestGetTx()
        {
            //await GetBlock();
            await TestContract();
            // var txHash = "0xed4642cc91eec83dc0ebbe809ff575211fd72951b8deab5c2e642d1f35e8f86f";

            // using (var client = new WebSocketClient(_ethSettings.Node))
            // {
            //     var web3 = new Web3(client);
            //     var tx = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);
            //     PrintTx(tx);
            // }
        }


        public async Task TestSubscriptions()
        {
            _logger.LogInformation("Test subscriptions");
            using (var client = new StreamingWebSocketClient(_ethSettings.Node))
            {
                var subscription = new EthNewBlockHeadersObservableSubscription(client);

                subscription.GetSubscribeResponseAsObservable()
                    .Subscribe(subscriptionId => _logger.LogInformation($"Block header subscription Id : {subscriptionId} "));

                DateTime? lastBlockNotification = null;
                double secondsSinceLastBlock = 0;

                // attach a handler for each block
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
                {
                    secondsSinceLastBlock = (lastBlockNotification == null) ? 0 : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                    lastBlockNotification = DateTime.Now;
                    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
                    Console.WriteLine($"New Block. Number: {block.Number.Value}, Timestamp UTC: {JsonConvert.SerializeObject(utcTimestamp)}, Seconds since last block received: {secondsSinceLastBlock} ");
                });

                bool subscribed = true;

                // handle unsubscription
                // optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    Console.WriteLine("Block Header unsubscribe result: " + response);
                });

                // open the websocket connection
                await client.StartAsync();

                // start the subscription
                // this will only block long enough to register the subscription with the client
                // once running - it won't block whilst waiting for blocks
                // blocks will be delivered to our handler on another thread
                await subscription.SubscribeAsync();

                // run for a minute before unsubscribing
                await Task.Delay(TimeSpan.FromMinutes(1));
                Console.WriteLine("subscription test delay done");

                // unsubscribe
                await subscription.UnsubscribeAsync();

                //allow time to unsubscribe
                while (subscribed) await Task.Delay(TimeSpan.FromSeconds(1));

            }
        }

        public async Task TestTxHistory()
        {
            using (var client = new WebSocketClient(_ethSettings.Node))
            {
                var contractAddress = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
                var web3 = new Web3(client);
                var filter = new NewFilterInput()
                {
                    Address = new string[] { contractAddress },
                    FromBlock = new BlockParameter(1),
                    ToBlock = BlockParameter.CreateLatest()
                };
                var result = await web3.Eth.Filters.NewFilter.SendRequestAsync(filter);

                _logger.LogInformation($"Result Tx History {result}");

                var lastBlock = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());

            }
        }

        public async Task<bool> Verify()
        {
            try
            {
                var contractAddress = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
                using (var clientws = new WebSocketClient(_ethSettings.Node))
                {
                    var web3 = new Web3(clientws);
                    var balance = await web3.Eth.GetBalance.SendRequestAsync(contractAddress);
                    _logger.LogInformation($"Balance of Uniswap contract is {balance}");

                    // get transaction count
                    var count = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(contractAddress);
                    _logger.LogInformation($"Tx Count is {count}");

                    // get block
                    var lastBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    _logger.LogInformation($"Last block is {lastBlock}");
                }

                return true;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Error getting balance");
                throw;
            }
        }
    }
}