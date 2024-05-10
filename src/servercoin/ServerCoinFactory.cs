using chia.dotnet;
using chia.dotnet.bls;
using chia.dotnet.clvm;
using chia.dotnet.wallet;
using System.Numerics;

namespace dig.servercoin;

public class ServerCoinFactory
{
    private readonly FullNodeProxy _fullNode;
    private readonly IWallet _wallet;
    private readonly Program _curriedMirrorPuzzle;
    private readonly Program _curriedMirrorPuzzleFromHex;

    public ServerCoinFactory(FullNodeProxy fullNode, IWallet wallet)
    {
        _fullNode = fullNode;
        _wallet = wallet;
        _curriedMirrorPuzzle = Puzzles.LoadPuzzle("p2_parent").Curry([Program.FromInt(1)]);
        _curriedMirrorPuzzleFromHex = Program.FromHex(_curriedMirrorPuzzle.HashHex().Remove0x());
    }

    public async Task<SpendBundle> CreateServerCoin(string storeId, IEnumerable<Uri> urls, string genesisChallenge, ulong amount, ulong fee, CancellationToken token = default)
    {
        var launcher = Program.FromHex(storeId);
        var hint = launcher.ToHint();

        var coinRecords = await _wallet.SelectCoinRecords(amount + fee, CoinSelection.Smallest, cancellationToken: token);
        if (coinRecords.Count == 0)
        {
            throw new Exception("Insufficient balance");
        }

        var totalValue = coinRecords.Aggregate(BigInteger.Zero, (acc, record) => acc + record.Coin.Amount);
        var changeAmount = totalValue - fee - amount;
        var urlPrograms = urls.Select(url => Program.FromText(url.ToString()));
        var coinSpends = coinRecords.Select((coinRecord, index) =>
        {
            var solution = new List<Program>();
            if (index == 0)
            {
                solution.Add(Program.FromSource($"({(int)ConditionCodes.CREATE_COIN} 0x{_curriedMirrorPuzzle.HashHex()} {amount} (0x{hint} {string.Join(" ", urlPrograms)}))"));
                solution.Add(Program.FromSource($"({(int)ConditionCodes.CREATE_COIN} {coinRecord.Coin.PuzzleHash.FormatAsExplicitHex()} {changeAmount})"));
            }

            var spentPuzzle = _wallet.FindProgram(coinRecord.Coin.PuzzleHash);
            return new CoinSpend
            {
                Coin = coinRecord.Coin,
                PuzzleReveal = spentPuzzle.SerializeHex(),
                Solution = StandardTransaction.GetSolution(solution).SerializeHex(),
            };
        }).ToList();

        var signedSpendBundle = _wallet.SignSpend(new SpendBundle
        {
            CoinSpends = coinSpends,
            AggregatedSignature = G2Element.GetInfinity().ToHex(),
        },
        genesisChallenge.ToHexBytes());

        return await _fullNode.PushTx(signedSpendBundle, token) ? signedSpendBundle : throw new Exception("Failed to push transaction");
    }

    public async Task<bool> DeleteServerCoin(string coinId, string genesisChallenge, ulong fee, CancellationToken token = default)
    {
        var coinRecordResponse = await _fullNode.GetCoinRecordByName(coinId, token);
        var puzzleSolution = await _fullNode.GetPuzzleAndSolution(coinRecordResponse.Coin.ParentCoinInfo, coinRecordResponse.ConfirmedBlockIndex, token);
        var revealProgram = Program.DeserializeHex(puzzleSolution.PuzzleReveal.Remove0x());
        var delegatedPuzzle = chia.dotnet.wallet.Puzzles.PayToConditions.Run(Program.FromList([Program.Nil])).Value;
        var standardTransactionInnerSolution = Program.FromList([
            Program.Nil,
            delegatedPuzzle,
            Program.Nil,
        ]);

        var coinRecords = await _wallet.SelectCoinRecords(1 + fee, CoinSelection.Smallest, cancellationToken: token);
        if (coinRecords.Count == 0)
        {
            throw new Exception("Insufficient balance");
        }

        var totalValue = coinRecords.Aggregate(BigInteger.Zero, (acc, record) => acc + record.Coin.Amount);
        var changeAmount = totalValue - fee;
        var coinSpends = coinRecords.Select((coinRecord, index) =>
        {
            var solution = new List<Program>();

            if (index == 0)
            {
                // Send the change to the same address
                solution.Add(
                    Program.FromSource($"({(int)ConditionCodes.CREATE_COIN} {coinRecord.Coin.PuzzleHash.FormatAsExplicitHex()} {changeAmount})")
                );
            }

            var spentPuzzle = _wallet.FindProgram(coinRecord.Coin.PuzzleHash);
            return new CoinSpend
            {
                Coin = coinRecord.Coin,
                PuzzleReveal = spentPuzzle.SerializeHex(),
                Solution = StandardTransaction.GetSolution(solution).SerializeHex(),
            };
        }).ToList();

        var deleteCoinSpend = new CoinSpend
        {
            Coin = coinRecordResponse.Coin,
            PuzzleReveal = _curriedMirrorPuzzle.SerializeHex(),
            Solution = Program.FromSource(
                $"({puzzleSolution.Coin.ParentCoinInfo} {revealProgram} {puzzleSolution.Coin.Amount} {standardTransactionInnerSolution})"
            ).SerializeHex(),
        };

        coinSpends.Add(deleteCoinSpend);

        var aggSigMeExtraData = genesisChallenge.ToHexBytes();
        var signedSpendBundle = _wallet.SignSpend(new SpendBundle
        {
            CoinSpends = coinSpends,
            AggregatedSignature = G2Element.GetInfinity().ToHex(),
        },
        genesisChallenge.ToHexBytes());

        return await _fullNode.PushTx(signedSpendBundle, token);
    }

    public async Task<IEnumerable<ServerCoin>> GetServerCoins(string storeId, CancellationToken token = default)
    {
        var launcher = Program.FromHex(storeId);
        var coinRecords = await _fullNode.GetCoinRecordsByHint(launcher.ToHint(), false, cancellationToken: token);
        var servers = new List<ServerCoin>();

        foreach (var coinRecord in coinRecords)
        {
            var puzzleSolution = await _fullNode.GetPuzzleAndSolution(
                coinRecord.Coin.ParentCoinInfo,
                coinRecord.ConfirmedBlockIndex,
                token
            );

            var revealProgram = Program.DeserializeHex(puzzleSolution.PuzzleReveal.Remove0x());
            var solutionProgram = Program.DeserializeHex(puzzleSolution.Solution.Remove0x());
            var conditions = revealProgram.Run(solutionProgram).Value;
            var createCoinConditions = conditions.ToList().Where(condition =>
                condition.ToList().Count == 4 &&
                condition.Rest.First.Equals(_curriedMirrorPuzzleFromHex) &&
                condition.First.ToInt() == (int)ConditionCodes.CREATE_COIN
            ).ToList();

            var urlString = createCoinConditions.Select(condition => condition.Rest.Rest.Rest.First.Rest);
            var urls = urlString.First().ToList().Select(url => url.ToText());
            var ourPuzzle = _wallet.IsOurs(revealProgram);

            servers.Add(new ServerCoin
            {
                Amount = coinRecord.Coin.Amount,
                CoinId = coinRecord.Coin.CoinId.ToHex().Remove0x(),
                LauncherId = launcher.ToHex().Remove0x(),
                Ours = ourPuzzle,
                Urls = urls,
            });
        }

        return servers;
    }
}
