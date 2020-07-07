/* This script gets all addresses ever used on the Veriblock blockchain
 * then counts all balances of those addresses and compares is to the 
 * amount mentioned by the networkstats. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitcoinVSVeriblockResultsCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start_time = DateTime.Now;
            decimal totalBalanceCalculatedFromAddresses = 0;
            int intCurrentVeriblockBlock = GetLatestVeriblock();
            List<string> listAddresses = new List<string> ();

            Console.WriteLine("Make sure your Nodecore is connected to the network and synced!");
            Console.WriteLine("");
            // Start collecting data from Veriblock blockchain
            Console.WriteLine("Collecting addresses from Veriblock Blockchain:");
            for (int i = 0; i < intCurrentVeriblockBlock; i++)
            {
                GetBlock(i, listAddresses);
                listAddresses = listAddresses.Distinct().ToList();
                DateTime current_time = DateTime.Now;
                TimeSpan time_diff = current_time - start_time;
                Console.Write("\r{0} of {1} Veriblock blocks processed. {2} unique adresses found. Script running for {3}", i + 1, intCurrentVeriblockBlock, listAddresses.Count, time_diff.ToString(@"hh\:mm\:ss"));
            }

            for (int i = 0; i < listAddresses.Count; i++)
            {
                decimal addressBalance = GetBalance(listAddresses[i]);
                totalBalanceCalculatedFromAddresses = totalBalanceCalculatedFromAddresses + addressBalance;
                Console.Write("\r{0} of {1} Veriblock adresses processed. {2} total balance thus far", i + 1, listAddresses.Count, totalBalanceCalculatedFromAddresses);
            }
            decimal totalBalanceFromStats = GetVeriblockStats();
            Console.WriteLine("");
            Console.WriteLine("Total balance from network stats: " + (totalBalanceFromStats / 100000000) + " VBK at block " + intCurrentVeriblockBlock);
            Console.WriteLine();
            Console.WriteLine("Results:");
            Console.WriteLine("Total balance calculated from addresses: " + (totalBalanceCalculatedFromAddresses));
            Console.WriteLine("Total balance from network stats: " + totalBalanceFromStats / 100000000);
            Console.WriteLine("Difference: " + ((totalBalanceCalculatedFromAddresses) - (totalBalanceFromStats / 100000000)));
            Console.WriteLine("");
            Console.WriteLine("Please note, a bit of difference is expected because the balance from network stats is updated once in ~15 minutes.");
            Console.ReadLine();
        }

        static public string TalkToNode(string json)
        {
            using (var webClient = new WebClient())
                try
                {
                    string response = webClient.UploadString("http://127.0.0.1:10600/api", "POST", json);
                    return response;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Error in TalkToNode: {1} \n {0} \n {1} ", exception);
                    return exception.ToString();
                }
        }

        static public int GetLatestVeriblock()
        {
            string jsonBlock = "{\"jsonrpc\": \"2.0\",\"method\": \"getinfo\",\"params\": {},\"id\": 1}";
            int latestVeriblock = 0;
            try
            {
                string response = TalkToNode(jsonBlock);
                JToken token = JObject.Parse(response);
                latestVeriblock = (int)token.SelectToken("result.lastBlock.number");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception: {exception}");
            }
            return latestVeriblock;
        }

        static public decimal GetVeriblockStats()
        {
            decimal totalSupply = 0;
            try
            {
                var client = new WebClient();
                var response = client.DownloadString("https://explore.veriblock.org/api/stats/summary");
                JToken token = JObject.Parse(response);
                decimal totalPowAfterBlock1 = decimal.Parse((string)token.SelectToken("supply.totalPowAfterBlock1"));
                decimal totalPopAfterBlock1 = decimal.Parse((string)token.SelectToken("supply.totalPopAfterBlock1"));
                decimal totalReflectedInBlock1 = decimal.Parse((string)token.SelectToken("supply.totalReflectedInBlock1"));
                totalSupply = totalPowAfterBlock1 + totalPopAfterBlock1 + totalReflectedInBlock1;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception: {exception}");
            }
            return totalSupply;
        }

        static public decimal GetBalance(string address)
        {
            decimal balance = 0;
            string url1 = "{\"jsonrpc\": \"2.0\",\"method\": \"getbalance\",\"params\": {\"addresses\": [";
            string url3 = "]},\"id\": 1}";
            string jsonBlock = String.Concat(url1, address, url3);
            string response = TalkToNode(jsonBlock);
            Getbalance result = JsonConvert.DeserializeObject<Getbalance>(response);
            ResultGetbalance ResultGetbalance = result.ResultGetbalance;
            if (result.ResultGetbalance != null)
            {
                Confirmed[] confirmedList = ResultGetbalance.Confirmed;
                Confirmed Confirmed = confirmedList[0];
                string totalConfirmed = Confirmed.TotalAmount;
                if (totalConfirmed != null)
                {
                    balance = decimal.Parse(totalConfirmed) / 100000000;
                }
            }
            return balance;
        }

        static public void GetBlock(int block_number, List<string> listAddresses)
        {
            string url1 = "{\"jsonrpc\": \"2.0\",\"method\": \"getblocks\",\"params\": {\"searchLength\": 1,\"filters\": [{\"index\": ";
            string url3 = "}]},\"id\": 1}";
            string jsonBlock = String.Concat(url1, block_number, url3);
            string response = TalkToNode(jsonBlock);
            VbkBlock items = JsonConvert.DeserializeObject<VbkBlock>(response);
            Result result = items.Result;
            if (result.Blocks != null)
            {
                Block[] blockArr = result.Blocks;
                Block block = blockArr[0];
                RegularTransaction[] regularTransactions = block.RegularTransactions;
                if (block.RegularTransactions != null)
                {
                    for (int i = 0; i < regularTransactions.Length; i++)
                    {
                        RegularTransaction RegularTransaction = regularTransactions[i];
                        Signed signed = RegularTransaction.Signed;
                        if (RegularTransaction.Signed != null)
                        {
                            Transaction transaction = signed.Transaction;
                            // save the source address from the regular transaction
                            listAddresses.Add(transaction.SourceAddress);
                            Output[] Txoutputs = transaction.Outputs;
                            if (transaction.Outputs != null)
                            {
                                for (int j = 0; j < Txoutputs.Length; j++)
                                {
                                    Output output = Txoutputs[j];
                                    // save the output addresses from the regular transaction
                                    listAddresses.Add(output.Address);
                                }
                            }
                        }
                        SignedMultisig signedMultisig = RegularTransaction.SignedMultisig;
                        if (RegularTransaction.SignedMultisig != null)
                        {
                            Transaction transaction1 = signedMultisig.Transaction;
                            // save the source address from the multisig transaction
                            listAddresses.Add(transaction1.SourceAddress);
                            Output[] Txoutputs1 = transaction1.Outputs;
                            if (transaction1.Outputs != null)
                            {
                                for (int j = 0; j < Txoutputs1.Length; j++)
                                {
                                    Output output = Txoutputs1[j];
                                    // save the output addresses from the multisig transaction
                                    listAddresses.Add(output.Address);
                                }
                            }
                        }
                    }
                }
                BlockContentMetapackage BlockContentMetapackage = block.BlockContentMetapackage;
                CoinbaseTransaction CoinbaseTransaction = BlockContentMetapackage.CoinbaseTransaction;
                Output[] PowOutputs = CoinbaseTransaction.PowOutputs;
                if (CoinbaseTransaction.PowOutputs != null)
                { 
                    for (int i = 0; i < PowOutputs.Length; i++)
                    {
                        Output output = PowOutputs[i];
                        // save the addresses from the PoW reward
                        listAddresses.Add(output.Address);
                    } 
                }
                Output[] PopOutputs = CoinbaseTransaction.PopOutputs;
                if (CoinbaseTransaction.PopOutputs != null)
                {
                    for (int i = 0; i < PopOutputs.Length; i++)
                    {
                        Output output = PopOutputs[i];
                        // save the addresses from the PoP reward
                        listAddresses.Add(output.Address);
                    }
                }
            }           
        }

        public partial class VbkBlock
        {
            [JsonProperty("jsonrpc")]
            public string Jsonrpc { get; set; }

            [JsonProperty("result")]
            public Result Result { get; set; }

            [JsonProperty("id")]
            public long Id { get; set; }
        }

        public partial class Result
        {
            [JsonProperty("blocks")]
            public Block[] Blocks { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }
        }

        public partial class Block
        {
            [JsonProperty("regularTransactions")]
            public RegularTransaction[] RegularTransactions { get; set; }

            [JsonProperty("blockContentMetapackage")]
            public BlockContentMetapackage BlockContentMetapackage { get; set; }

            [JsonProperty("number")]
            public long Number { get; set; }

            [JsonProperty("timestamp")]
            public long Timestamp { get; set; }

            [JsonProperty("hash")]
            public string Hash { get; set; }

            [JsonProperty("previousHash")]
            public string PreviousHash { get; set; }

            [JsonProperty("secondPreviousHash")]
            public string SecondPreviousHash { get; set; }

            [JsonProperty("thirdPreviousHash")]
            public string ThirdPreviousHash { get; set; }

            [JsonProperty("encodedDifficulty")]
            public long EncodedDifficulty { get; set; }

            [JsonProperty("winningNonce")]
            public long WinningNonce { get; set; }

            [JsonProperty("totalFees")]
            public long TotalFees { get; set; }

            [JsonProperty("powCoinbaseReward")]
            public string PowCoinbaseReward { get; set; }

            [JsonProperty("popCoinbaseReward")]
            public string PopCoinbaseReward { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }

            [JsonProperty("version")]
            public long Version { get; set; }

            [JsonProperty("merkleRoot")]
            public string MerkleRoot { get; set; }
        }

        public partial class BlockContentMetapackage
        {
            [JsonProperty("coinbaseTransaction")]
            public CoinbaseTransaction CoinbaseTransaction { get; set; }

            [JsonProperty("popDatastore")]
            public PopDatastore PopDatastore { get; set; }

            [JsonProperty("blockFeeTable")]
            public BlockFeeTable BlockFeeTable { get; set; }

            [JsonProperty("ledgerHash")]
            public string LedgerHash { get; set; }

            [JsonProperty("extraNonce")]
            public string ExtraNonce { get; set; }

            [JsonProperty("hash")]
            public string Hash { get; set; }
        }

        public partial class BlockFeeTable
        {
            [JsonProperty("popFeeShare")]
            public long PopFeeShare { get; set; }
        }

        public partial class CoinbaseTransaction
        {
            [JsonProperty("powOutputs")]
            public Output[] PowOutputs { get; set; }

            [JsonProperty("popOutputs")]
            public Output[] PopOutputs { get; set; }

            [JsonProperty("powCoinbaseAmount")]
            public string PowCoinbaseAmount { get; set; }

            [JsonProperty("popCoinbaseAmount")]
            public string PopCoinbaseAmount { get; set; }

            [JsonProperty("powFeeShare")]
            public long PowFeeShare { get; set; }

            [JsonProperty("popFeeShare")]
            public long PopFeeShare { get; set; }

            [JsonProperty("blockHeight")]
            public long BlockHeight { get; set; }

            [JsonProperty("txId")]
            public string TxId { get; set; }
        }

        public partial class Output
        {
            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonProperty("amount")]
            public string Amount { get; set; }
        }

        public partial class PopDatastore
        {
            [JsonProperty("datastoreHash")]
            public string DatastoreHash { get; set; }

            [JsonProperty("endorsedVeriblockHeadersHash")]
            public string EndorsedVeriblockHeadersHash { get; set; }

            [JsonProperty("endorsedAltchainHeadersHash")]
            public string EndorsedAltchainHeadersHash { get; set; }
        }

        public partial class RegularTransaction
        {
            [JsonProperty("signed")]
            public Signed Signed { get; set; }
        }

        public partial class Signed
        {
            [JsonProperty("transaction")]
            public Transaction Transaction { get; set; }

            [JsonProperty("signature")]
            public string Signature { get; set; }

            [JsonProperty("publicKey")]
            public string PublicKey { get; set; }

            [JsonProperty("signatureIndex")]
            public long SignatureIndex { get; set; }
        }

        public partial class Transaction
        {
            [JsonProperty("outputs")]
            public Output[] Outputs { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("sourceAddress")]
            public string SourceAddress { get; set; }

            [JsonProperty("sourceAmount")]
            public string SourceAmount { get; set; }

            [JsonProperty("transactionFee")]
            public long TransactionFee { get; set; }

            [JsonProperty("timestamp")]
            public long Timestamp { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }

            [JsonProperty("txId")]
            public string TxId { get; set; }
        }

        public partial class Getbalance
        {
            [JsonProperty("jsonrpc")]
            public string Jsonrpc { get; set; }

            [JsonProperty("result")]
            public ResultGetbalance ResultGetbalance { get; set; }

            [JsonProperty("id")]
            public long Id { get; set; }
        }

        public partial class ResultGetbalance
        {
            [JsonProperty("confirmed")]
            public Confirmed[] Confirmed { get; set; }

            [JsonProperty("unconfirmed")]
            public List<Unconfirmed> Unconfirmed { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("totalConfirmed")]
            public string TotalConfirmed { get; set; }
        }

        public partial class Confirmed
        {
            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonProperty("unlockedAmount")]
            public long UnlockedAmount { get; set; }

            [JsonProperty("lockedAmount")]
            public string LockedAmount { get; set; }

            [JsonProperty("totalAmount")]
            public string TotalAmount { get; set; }
        }

        public partial class Unconfirmed
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }


        public partial class RegularTransaction
        {
            [JsonProperty("signedMultisig")]
            public SignedMultisig SignedMultisig { get; set; }
        }

        public partial class SignedMultisig
        {
            [JsonProperty("signatureBundle")]
            public SignatureBundle SignatureBundle { get; set; }

            [JsonProperty("transaction")]
            public Transaction Transaction { get; set; }

            [JsonProperty("signatureIndex")]
            public long SignatureIndex { get; set; }
        }

        public partial class SignatureBundle
        {
            [JsonProperty("slots")]
            public List<Slot> Slots { get; set; }
        }

        public partial class Slot
        {
            [JsonProperty("populated", NullValueHandling = NullValueHandling.Ignore)]
            public bool? Populated { get; set; }

            [JsonProperty("signature", NullValueHandling = NullValueHandling.Ignore)]
            public string Signature { get; set; }

            [JsonProperty("publicKey", NullValueHandling = NullValueHandling.Ignore)]
            public string PublicKey { get; set; }

            [JsonProperty("ownerAddress", NullValueHandling = NullValueHandling.Ignore)]
            public string OwnerAddress { get; set; }
        }
    }
}
