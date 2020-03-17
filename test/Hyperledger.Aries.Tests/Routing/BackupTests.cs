using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Routing;
using Hyperledger.TestHarness.Mock;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Extensions;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.WalletApi;
using Microsoft.Extensions.Options;
using Xunit;
using Hyperledger.Aries.Decorators.Attachments;

namespace Hyperledger.Aries.Tests.Routing
{
    public class BackupTests : IAsyncLifetime
    {
        public InProcAgent.PairedAgents Pair { get; private set; }
        
        public IEdgeClientService EdgeClient { get; private set; }
        public IEdgeClientService RetrieveEdgeClient { get; private set; }
        
        public IAgentContext EdgeContext { get; private set; }
        public IAgentContext RetrieverEdgeContext { get; private set; }
        
        public AgentOptions AgentOptions { get; private set; }
        public AgentOptions RetrieverAgentOptions { get; private set; }
        
        public IAgentContext MediatorContext { get; private set; }

        public async Task DisposeAsync()
        {
            await Pair.Agent1.DisposeAsync();
            await Pair.Agent2.DisposeAsync();
            await Pair.Agent3.DisposeAsync();
        }

        public async Task InitializeAsync()
        {
            // Agent1 - Mediator
            // Agent2 - Edge
            // Agent3 - RetrieveEdge
            Pair = await InProcAgent.CreateTwoWalletsPairedWithRoutingAsync();
            
            EdgeClient = Pair.Agent2.Host.Services.GetRequiredService<IEdgeClientService>();
            RetrieveEdgeClient = Pair.Agent3.Host.Services.GetRequiredService<IEdgeClientService>();
            
            AgentOptions = Pair.Agent2.Host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
            RetrieverAgentOptions = Pair.Agent3.Host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
            
            EdgeContext = Pair.Agent2.Context;
            RetrieverEdgeContext = Pair.Agent3.Context;
            
            MediatorContext = Pair.Agent1.Context;
        }

        [Fact(DisplayName = "Create backup with default seed")]
        public async Task CreateBackup()
        {
            var seed = "00000000000000000000000000000000";

            var path = SetupDirectoriesAndReturnPath(seed);
            
            var r = await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            var numDirsAfterBackup = Directory.GetDirectories(path).Length;
            var walletDir = Directory.GetDirectories(path).First();
            var backupDir = Directory.GetDirectories(walletDir).First();
            var backedUpWallet = Directory.GetFiles(backupDir).First();
            
            Assert.True(Directory.Exists(path));
            Assert.True(numDirsAfterBackup > 0);
            Assert.True(File.Exists(backedUpWallet));
        }
        
        [Fact(DisplayName = "Create backup with shorter seed throws ArgumentException")]
        public async Task CreateBackupWithShortSeed()
        {
            var seed = "11112222";
            SetupDirectoriesAndReturnPath(seed);
            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => EdgeClient.CreateBackupAsync(EdgeContext, seed));
            Assert.Equal(ex.Message, $"{nameof(seed)} should be 32 characters");
        }

        [Fact(DisplayName = "Get a list of available backups")]
        public async Task ListBackups()
        {
            // Wait one second
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            var seed = "00000000000000000000000000000000";
            SetupDirectoriesAndReturnPath(seed);

            var backupId = await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            var result = await EdgeClient.ListBackupsAsync(EdgeContext, backupId);

            Assert.NotEmpty(result);
        }

        [Fact(DisplayName = "Retrieve latest backup")]
        public async Task RetrieveLatestBackup()
        {
            var seed = "00000000000000000000000000000000";

            SetupDirectoriesAndReturnPath(seed);
            await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            
            var result = await RetrieveEdgeClient.RetrieveBackupAsync(RetrieverEdgeContext, seed);
            
            Assert.NotEmpty(result);
            Assert.IsType<Attachment>(result.First());
        }

        [Fact(DisplayName = "Restore edge agent from backup")]
        public async Task RestoreAgentFromBackup()
        {
            var seed = "00000000000000000000000000000000";
            // Create a DID that we will retrieve and compare from imported wallet
            var myDid = await Did.CreateAndStoreMyDidAsync(EdgeContext.Wallet, "{}");

            // Create backup
            await EdgeClient.CreateBackupAsync(EdgeContext, seed);

            // Retrieve and restore
            var attachments = await RetrieveEdgeClient.RetrieveBackupAsync(RetrieverEdgeContext, seed);
            await RetrieveEdgeClient.RestoreFromBackupAsync(RetrieverEdgeContext, seed, attachments);

            // Get new context, to refresh the wallet handle, old one has been disposed
            var context = await Pair.Agent3.Host.Services.GetRequiredService<IAgentProvider>().GetContextAsync();
            var myKey = await Did.KeyForLocalDidAsync(context.Wallet, myDid.Did);
            Assert.Equal(myKey, myDid.VerKey);
            
            // TODO: Add response
            // TODO: Add assertsions
        }

        private string SetupDirectoriesAndReturnPath(string seed)
        {
            var edgeWallet = Path.Combine(Path.GetTempPath(), seed);

            if (File.Exists(edgeWallet))
            {
                File.Delete(edgeWallet);
            }
            
            var path = Path.Combine(Path.GetTempPath(), "AriesWallets");

            var walletDirExists = Directory.Exists(path);

            if (walletDirExists)
            {
                Directory.Delete(path, true);
            }

            return path;
        }
    }
}
