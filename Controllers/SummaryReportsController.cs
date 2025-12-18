using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;
using System.Globalization;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SummaryReportsController : BaseController
    {
        private readonly ClearBooksDbContext _context;
        private readonly ILogger<SummaryReportsController> _logger;

        public SummaryReportsController(ClearBooksDbContext context, ILogger<SummaryReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        //main endpoint that powers the financial dashboard
        [HttpGet("Dashboard")]
        public async Task<ActionResult<DashboardData>> GetDashboardData()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var today = DateTime.Now;
                var startOfYear = new DateTime(today.Year, 1, 1);

                var dashboardData = new DashboardData
                {
                    FinancialOverview = await GetFinancialOverview(userId, today),
                    AccountDistribution = await GetAccountDistribution(userId, today),
                    MonthlyTrends = await GetMonthlyTrends(userId, 12),
                    CashFlowAnalysis = await GetCashFlowAnalysis(userId, 6),
                    TopAccounts = await GetTopAccounts(userId, "Expense", 10),
                    TransactionVolume = await GetTransactionVolume(userId, 12),
                    BalanceSheetSummary = await GetBalanceSheetSummary(userId, today),
                    FinancialSummary = await GetFinancialSummary(userId, startOfYear, today)
                };

                return Ok(dashboardData);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating dashboard data");
                return StatusCode(500, new { message = "Error generating dashboard data" });
            }
        }

        //getting mon thly trend data
        [HttpGet("MonthlyTrends")]
        public async Task<ActionResult<MonthlyTrendsData>> GetMonthlyTrends(
            [FromQuery] int months = 12)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var data = await GetMonthlyTrends(userId, months);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly trends");
                return StatusCode(500, new { message = "Error generating monthly trends" });
            }
        }

        //provides cash flow analysis
        [HttpGet("CashFlow")]
        public async Task<ActionResult<CashFlowData>> GetCashFlow(
            [FromQuery] int months = 6)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var data = await GetCashFlowAnalysis(userId, months);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cash flow analysis");
                return StatusCode(500, new { message = "Error generating cash flow analysis" });
            }
        }

        //shows top accounts by balance
        [HttpGet("TopAccounts")]
        public async Task<ActionResult<TopAccountsData>> GetTopAccounts(
            [FromQuery] string accountType = "Expense",
            [FromQuery] int count = 10)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var data = await GetTopAccounts(userId, accountType, count);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating top accounts data");
                return StatusCode(500, new { message = "Error generating top accounts data" });
            }
        }

        //analayzes transaction volume over time 
        [HttpGet("TransactionVolume")]
        public async Task<ActionResult<TransactionVolumeData>> GetTransactionVolume(
            [FromQuery] int months = 12,
            [FromQuery] string metric = "count")
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var data = await GetTransactionVolume(userId, months, metric);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transaction volume data");
                return StatusCode(500, new { message = "Error generating transaction volume data" });
            }
        }

        //balance distribution across different account typess
        [HttpGet("AccountDistribution")]
        public async Task<ActionResult<AccountDistributionData>> GetAccountDistribution()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var data = await GetAccountDistribution(userId, DateTime.Now);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating account distribution data");
                return StatusCode(500, new { message = "Error generating account distribution data" });
            }
        }

        // private Helper Methods
        private async Task<FinancialOverviewData> GetFinancialOverview(int userId, DateTime asOfDate)
        {
            //get balance sheet data
            var assetAccounts = await GetAccountBalances(userId, "Asset", asOfDate);
            var liabilityAccounts = await GetAccountBalances(userId, "Liability", asOfDate);
            var equityAccounts = await GetAccountBalances(userId, "Equity", asOfDate);

            //get income statement data for current year
            var startOfYear = new DateTime(asOfDate.Year, 1, 1);
            var revenueData = await GetAccountBalances(userId, "Revenue", asOfDate, startOfYear);
            var expenseData = await GetAccountBalances(userId, "Expense", asOfDate, startOfYear);

            var totalAssets = assetAccounts.Sum(a => a.Balance);
            var totalLiabilities = liabilityAccounts.Sum(l => l.Balance);
            var totalEquity = equityAccounts.Sum(e => e.Balance);
            var totalRevenue = revenueData.Sum(r => r.Balance);
            var totalExpenses = expenseData.Sum(e => e.Balance);
            var netIncome = totalRevenue - totalExpenses;

            return new FinancialOverviewData
            {
                TotalAssets = totalAssets,
                TotalLiabilities = totalLiabilities,
                TotalEquity = totalEquity + netIncome, //add retained earnings
                NetIncome = netIncome,
                AsOfDate = asOfDate
            };
        }

        private async Task<AccountDistributionData> GetAccountDistribution(int userId, DateTime asOfDate)
        {
            var accountTypes = new[] { "Asset", "Liability", "Equity", "Revenue", "Expense" };
            var distribution = new List<AccountTypeBalance>();

            foreach (var accountType in accountTypes)
            {
                var accounts = await GetAccountBalances(userId, accountType, asOfDate);
                var totalBalance = accounts.Sum(a => Math.Abs(a.Balance));

                if (totalBalance > 0)
                {
                    distribution.Add(new AccountTypeBalance
                    {
                        AccountType = accountType,
                        Balance = totalBalance
                    });
                }
            }

            return new AccountDistributionData
            {
                Distribution = distribution,
                AsOfDate = asOfDate
            };
        }

        private async Task<MonthlyTrendsData> GetMonthlyTrends(int userId, int months)
        {
            var monthlyData = new List<MonthlyDataPoint>();
            var currentDate = DateTime.Now;

            for (int i = months - 1; i >= 0; i--)
            {
                var targetDate = currentDate.AddMonths(-i);
                var startOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                var revenueAccounts = await GetAccountBalances(userId, "Revenue", endOfMonth, startOfMonth);
                var expenseAccounts = await GetAccountBalances(userId, "Expense", endOfMonth, startOfMonth);

                var monthlyRevenue = revenueAccounts.Sum(a => a.Balance);
                var monthlyExpenses = expenseAccounts.Sum(a => a.Balance);

                monthlyData.Add(new MonthlyDataPoint
                {
                    Month = startOfMonth.ToString("MMM yyyy"),
                    Revenue = monthlyRevenue,
                    Expenses = monthlyExpenses,
                    NetIncome = monthlyRevenue - monthlyExpenses
                });
            }

            return new MonthlyTrendsData
            {
                MonthlyData = monthlyData
            };
        }

        private async Task<CashFlowData> GetCashFlowAnalysis(int userId, int months)
        {
            var cashFlowData = new List<CashFlowDataPoint>();
            var currentDate = DateTime.Now;

            //get cash and bank accounts
            var cashAccounts = await _context.ChartOfAccounts
                .Where(a => a.UserId == userId &&
                           (a.AccountName.ToLower().Contains("cash") ||
                            a.AccountName.ToLower().Contains("bank") ||
                            a.AccountType == "Asset"))
                .Select(a => a.AccountId)
                .ToListAsync();

            for (int i = months - 1; i >= 0; i--)
            {
                var targetDate = currentDate.AddMonths(-i);
                var startOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                //calculate cash inflows (revenue and asset increases)
                var revenueData = await GetAccountBalances(userId, "Revenue", endOfMonth, startOfMonth);
                var cashInflows = revenueData.Sum(a => a.Balance);

                //calculate cash outflows (expenses and liability decreases)
                var expenseData = await GetAccountBalances(userId, "Expense", endOfMonth, startOfMonth);
                var cashOutflows = expenseData.Sum(a => a.Balance);

                cashFlowData.Add(new CashFlowDataPoint
                {
                    Month = startOfMonth.ToString("MMM yyyy"),
                    Inflow = cashInflows,
                    Outflow = cashOutflows,
                    NetFlow = cashInflows - cashOutflows
                });
            }

            return new CashFlowData
            {
                CashFlowPoints = cashFlowData
            };
        }

        private async Task<TopAccountsData> GetTopAccounts(int userId, string accountType, int count)
        {
            var accounts = await GetAccountBalances(userId, accountType, DateTime.Now);

            var topAccounts = accounts
                .OrderByDescending(a => Math.Abs(a.Balance))
                .Take(count)
                .Select(a => new TopAccountDataPoint
                {
                    AccountName = a.AccountName,
                    AccountNumber = a.AccountNumber,
                    Balance = Math.Abs(a.Balance)
                })
                .ToList();

            return new TopAccountsData
            {
                AccountType = accountType,
                TopAccounts = topAccounts
            };
        }

        private async Task<TransactionVolumeData> GetTransactionVolume(int userId, int months, string metric = "count")
        {
            var volumeData = new List<VolumeDataPoint>();
            var currentDate = DateTime.Now;

            for (int i = months - 1; i >= 0; i--)
            {
                var targetDate = currentDate.AddMonths(-i);
                var startOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                var monthlyVouchers = await _context.VoucherHeaders
                    .Where(v => v.UserId == userId &&
                               v.VoucherDate >= startOfMonth &&
                               v.VoucherDate <= endOfMonth &&
                               v.Status != "Void")
                    .ToListAsync();

                decimal value = 0;
                if (metric == "count")
                {
                    value = monthlyVouchers.Count;
                }
                else
                {
                    value = monthlyVouchers.Sum(v => v.TotalAmount);
                }

                volumeData.Add(new VolumeDataPoint
                {
                    Month = startOfMonth.ToString("MMM yyyy"),
                    Value = value
                });
            }

            return new TransactionVolumeData
            {
                Metric = metric,
                VolumePoints = volumeData
            };
        }

        private async Task<BalanceSheetSummaryData> GetBalanceSheetSummary(int userId, DateTime asOfDate)
        {
            var assets = await GetAccountBalances(userId, "Asset", asOfDate);
            var liabilities = await GetAccountBalances(userId, "Liability", asOfDate);
            var equity = await GetAccountBalances(userId, "Equity", asOfDate);

            return new BalanceSheetSummaryData
            {
                TotalAssets = assets.Sum(a => a.Balance),
                TotalLiabilities = liabilities.Sum(l => l.Balance),
                TotalEquity = equity.Sum(e => e.Balance),
                AsOfDate = asOfDate
            };
        }

        private async Task<FinancialSummaryData> GetFinancialSummary(int userId, DateTime fromDate, DateTime toDate)
        {
            var currentPeriodRevenue = await GetPeriodTotal(userId, "Revenue", fromDate, toDate);
            var currentPeriodExpenses = await GetPeriodTotal(userId, "Expense", fromDate, toDate);
            var currentPeriodNetIncome = currentPeriodRevenue - currentPeriodExpenses;

            //calculate previous period (same duration, previous year)
            var previousFromDate = fromDate.AddYears(-1);
            var previousToDate = toDate.AddYears(-1);
            var previousPeriodRevenue = await GetPeriodTotal(userId, "Revenue", previousFromDate, previousToDate);
            var previousPeriodExpenses = await GetPeriodTotal(userId, "Expense", previousFromDate, previousToDate);
            var previousPeriodNetIncome = previousPeriodRevenue - previousPeriodExpenses;

            //get current balance sheet totals
            var currentAssets = await GetAccountTypeTotal(userId, "Asset", toDate);
            var previousAssets = await GetAccountTypeTotal(userId, "Asset", previousToDate);

            return new FinancialSummaryData
            {
                CurrentPeriod = new PeriodSummary
                {
                    Revenue = currentPeriodRevenue,
                    Expenses = currentPeriodExpenses,
                    NetIncome = currentPeriodNetIncome,
                    Assets = currentAssets
                },
                PreviousPeriod = new PeriodSummary
                {
                    Revenue = previousPeriodRevenue,
                    Expenses = previousPeriodExpenses,
                    NetIncome = previousPeriodNetIncome,
                    Assets = previousAssets
                },
                FromDate = fromDate,
                ToDate = toDate
            };
        }

        private async Task<List<AccountBalance>> GetAccountBalances(int userId, string accountType, DateTime asOfDate, DateTime? fromDate = null)
        {
            var accounts = await _context.ChartOfAccounts
                .Where(a => a.UserId == userId && a.AccountType == accountType)
                .ToListAsync();

            var accountBalances = new List<AccountBalance>();

            foreach (var account in accounts)
            {
                var transactions = await _context.VoucherDetails
                    .Include(vd => vd.VoucherHeader)
                    .Where(vd => vd.AccountId == account.AccountId &&
                               vd.VoucherHeader.UserId == userId &&
                               vd.VoucherHeader.VoucherDate <= asOfDate &&
                               (fromDate == null || vd.VoucherHeader.VoucherDate >= fromDate) &&
                               vd.VoucherHeader.Status != "Void")
                    .ToListAsync();

                decimal balance = 0;
                foreach (var transaction in transactions)
                {
                    if (IsDebitAccount(accountType))
                    {
                        balance += transaction.IsDebit ? transaction.Amount : -transaction.Amount;
                    }
                    else
                    {
                        balance += transaction.IsDebit ? -transaction.Amount : transaction.Amount;
                    }
                }

                if (balance != 0 || fromDate != null) //include zero balances for period calculations
                {
                    accountBalances.Add(new AccountBalance
                    {
                        AccountId = account.AccountId,
                        AccountNumber = account.AccountNumber,
                        AccountName = account.AccountName,
                        AccountType = account.AccountType,
                        Balance = Math.Abs(balance) //use absolute value for summaries
                    });
                }
            }

            return accountBalances;
        }

        private async Task<decimal> GetPeriodTotal(int userId, string accountType, DateTime fromDate, DateTime toDate)
        {
            var accounts = await GetAccountBalances(userId, accountType, toDate, fromDate);
            return accounts.Sum(a => a.Balance);
        }

        private async Task<decimal> GetAccountTypeTotal(int userId, string accountType, DateTime asOfDate)
        {
            var accounts = await GetAccountBalances(userId, accountType, asOfDate);
            return accounts.Sum(a => a.Balance);
        }

        private bool IsDebitAccount(string accountType)
        {
            return accountType == "Asset" || accountType == "Expense";
        }
    }

    //DdTOs for Summary Reports
    public class DashboardData
    {
        public FinancialOverviewData FinancialOverview { get; set; }
        public AccountDistributionData AccountDistribution { get; set; }
        public MonthlyTrendsData MonthlyTrends { get; set; }
        public CashFlowData CashFlowAnalysis { get; set; }
        public TopAccountsData TopAccounts { get; set; }
        public TransactionVolumeData TransactionVolume { get; set; }
        public BalanceSheetSummaryData BalanceSheetSummary { get; set; }
        public FinancialSummaryData FinancialSummary { get; set; }
    }

    public class FinancialOverviewData
    {
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal NetIncome { get; set; }
        public DateTime AsOfDate { get; set; }
    }

    public class AccountDistributionData
    {
        public List<AccountTypeBalance> Distribution { get; set; } = new List<AccountTypeBalance>();
        public DateTime AsOfDate { get; set; }
    }

    public class AccountTypeBalance
    {
        public string AccountType { get; set; }
        public decimal Balance { get; set; }
    }

    public class MonthlyTrendsData
    {
        public List<MonthlyDataPoint> MonthlyData { get; set; } = new List<MonthlyDataPoint>();
    }

    public class MonthlyDataPoint
    {
        public string Month { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetIncome { get; set; }
    }

    public class CashFlowData
    {
        public List<CashFlowDataPoint> CashFlowPoints { get; set; } = new List<CashFlowDataPoint>();
    }

    public class CashFlowDataPoint
    {
        public string Month { get; set; }
        public decimal Inflow { get; set; }
        public decimal Outflow { get; set; }
        public decimal NetFlow { get; set; }
    }

    public class TopAccountsData
    {
        public string AccountType { get; set; }
        public List<TopAccountDataPoint> TopAccounts { get; set; } = new List<TopAccountDataPoint>();
    }

    public class TopAccountDataPoint
    {
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        public decimal Balance { get; set; }
    }

    public class TransactionVolumeData
    {
        public string Metric { get; set; }
        public List<VolumeDataPoint> VolumePoints { get; set; } = new List<VolumeDataPoint>();
    }

    public class VolumeDataPoint
    {
        public string Month { get; set; }
        public decimal Value { get; set; }
    }

    public class BalanceSheetSummaryData
    {
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public DateTime AsOfDate { get; set; }
    }

    public class FinancialSummaryData
    {
        public PeriodSummary CurrentPeriod { get; set; }
        public PeriodSummary PreviousPeriod { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class PeriodSummary
    {
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal Assets { get; set; }
    }

    public class AccountBalance
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public decimal Balance { get; set; }
    }
}