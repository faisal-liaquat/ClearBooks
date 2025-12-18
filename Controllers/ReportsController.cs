using ClearBooksFYP.Models;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Threading.Tasks;

using SelectPdf;



namespace ClearBooksFYP.Controllers

{

    [Route("api/[controller]")]

    [ApiController]

    public class ReportsController : BaseController

    {

        private readonly ClearBooksDbContext _context;

        private readonly ILogger<ReportsController> _logger;



        public ReportsController(ClearBooksDbContext context, ILogger<ReportsController> logger)

        {

            _context = context;

            _logger = logger;

        }



        //get general ledger
        // shows all accounting transactions in chronological order

        [HttpGet("GeneralLedger")]

        public async Task<ActionResult<object>> GetGeneralLedger(

            [FromQuery] DateTime fromDate,

            [FromQuery] DateTime toDate,

            [FromQuery] int? accountId = null)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                //validate date range

                if (fromDate > toDate)

                {

                    return BadRequest(new { message = "From date cannot be later than to date" });

                }



                var query = _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Include(vd => vd.ChartOfAccount)

                    .Where(vd => vd.VoucherHeader.UserId == userId &&

                                vd.VoucherHeader.VoucherDate >= fromDate &&

                                vd.VoucherHeader.VoucherDate <= toDate);



                if (accountId.HasValue)

                {

                    query = query.Where(vd => vd.AccountId == accountId.Value);

                }



                var entries = await query

                    .OrderBy(vd => vd.VoucherHeader.VoucherDate)

                    .ThenBy(vd => vd.VoucherHeader.VoucherId)

                    .Select(vd => new

                    {

                        date = vd.VoucherHeader.VoucherDate,

                        voucherNumber = vd.VoucherHeader.VoucherNumber,

                        accountNumber = vd.ChartOfAccount.AccountNumber,

                        accountName = vd.ChartOfAccount.AccountName,

                        description = vd.Description ?? vd.VoucherHeader.Description,

                        debitAmount = vd.IsDebit ? vd.Amount : 0,

                        creditAmount = !vd.IsDebit ? vd.Amount : 0,

                        balance = 0m //will be calculated

                    })

                    .ToListAsync();



                //calculate running balance

                decimal runningBalance = 0;

                var entriesWithBalance = entries.Select(entry =>

                {

                    runningBalance += entry.debitAmount - entry.creditAmount;

                    return new

                    {

                        entry.date,

                        entry.voucherNumber,

                        entry.accountNumber,

                        entry.accountName,

                        entry.description,

                        entry.debitAmount,

                        entry.creditAmount,

                        balance = runningBalance

                    };

                }).ToList();



                var totalDebits = entries.Sum(e => e.debitAmount);

                var totalCredits = entries.Sum(e => e.creditAmount);



                return Ok(new

                {

                    entries = entriesWithBalance,

                    totalDebits,

                    totalCredits,

                    fromDate,

                    toDate

                });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating general ledger report");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        //trial balance report
        //shows account balances at a specficic point in time to verify debit = credit

        [HttpGet("TrialBalance")]

        public async Task<ActionResult<object>> GetTrialBalance([FromQuery] DateTime asOfDate)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                var accountBalances = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Include(vd => vd.ChartOfAccount)

                    .Where(vd => vd.VoucherHeader.UserId == userId && vd.VoucherHeader.VoucherDate <= asOfDate)

                    .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })

                    .Select(g => new

                    {

                        accountNumber = g.Key.AccountNumber,

                        accountName = g.Key.AccountName,

                        accountType = g.Key.AccountType,

                        debitBalance = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount) > g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)

                            ? g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount) - g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)

                            : 0,

                        creditBalance = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount) > g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount)

                            ? g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount) - g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount)

                            : 0

                    })

                    .Where(ab => ab.debitBalance > 0 || ab.creditBalance > 0)

                    .OrderBy(ab => ab.accountNumber)

                    .ToListAsync();



                var totalDebits = accountBalances.Sum(ab => ab.debitBalance);

                var totalCredits = accountBalances.Sum(ab => ab.creditBalance);



                return Ok(new

                {

                    entries = accountBalances,

                    totalDebits,

                    totalCredits,

                    asOfDate

                });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating trial balance");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        //income statement report

        [HttpGet("IncomeStatement")]

        public async Task<ActionResult<object>> GetIncomeStatement(

            [FromQuery] DateTime fromDate,

            [FromQuery] DateTime toDate)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                if (fromDate > toDate)

                {

                    return BadRequest(new { message = "From date cannot be later than to date" });

                }



                var accountBalances = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Include(vd => vd.ChartOfAccount)

                    .Where(vd => vd.VoucherHeader.UserId == userId &&

                                vd.VoucherHeader.VoucherDate >= fromDate &&

                                vd.VoucherHeader.VoucherDate <= toDate)

                    .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })

                    .Select(g => new

                    {

                        accountId = g.Key.AccountId,

                        accountNumber = g.Key.AccountNumber,

                        accountName = g.Key.AccountName,

                        accountType = g.Key.AccountType,

                        debitSum = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount),

                        creditSum = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)

                    })

                    .ToListAsync();



                var revenues = new List<object>();

                var expenses = new List<object>();



                foreach (var account in accountBalances)

                {

                    if (IsRevenueAccount(account.accountType, account.accountNumber))

                    {

                        //revenue accounts typically have credit balances

                        var amount = account.creditSum - account.debitSum;

                        if (amount > 0)

                        {

                            revenues.Add(new

                            {

                                accountId = account.accountId,

                                accountNumber = account.accountNumber,

                                accountName = account.accountName,

                                amount

                            });

                        }

                    }

                    else if (IsExpenseAccount(account.accountType, account.accountNumber))

                    {

                        //expense accounts typically have debit balances

                        var amount = account.debitSum - account.creditSum;

                        if (amount > 0)

                        {

                            expenses.Add(new

                            {

                                accountId = account.accountId,

                                accountNumber = account.accountNumber,

                                accountName = account.accountName,

                                amount

                            });

                        }

                    }

                }



                var totalRevenue = revenues.Sum(r => (decimal)((dynamic)r).amount);

                var totalExpenses = expenses.Sum(e => (decimal)((dynamic)e).amount);

                var netIncome = totalRevenue - totalExpenses;



                return Ok(new

                {

                    revenues,

                    expenses,

                    totalRevenue,

                    totalExpenses,

                    netIncome,

                    fromDate,

                    toDate

                });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating income statement");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        //profit loss statement

        [HttpGet("ProfitLoss")]

        public async Task<ActionResult<object>> GetProfitLoss(

            [FromQuery] DateTime fromDate,

            [FromQuery] DateTime toDate,

            [FromQuery] string comparison = null)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                //date range 

                if (fromDate > toDate)

                {

                    return BadRequest(new { message = "From date cannot be later than to date" });

                }



                //date range cant be more than 10 years


                var daysDiff = (toDate - fromDate).Days;

                if (daysDiff > 3650) //10 years

                {

                    return BadRequest(new { message = "Date range cannot exceed 10 years" });

                }



                _logger.LogInformation($"Generating P&L report for user {userId}, period: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");



                //get the main period data

                var mainPeriodData = await GenerateProfitLossData(userId, fromDate, toDate);



                var response = new

                {

                    fromDate = fromDate,

                    toDate = toDate,

                    revenues = mainPeriodData.Revenues,

                    expenses = mainPeriodData.Expenses,

                    totalRevenue = mainPeriodData.TotalRevenue,

                    totalExpenses = mainPeriodData.TotalExpenses,

                    netIncome = mainPeriodData.NetIncome,

                    costOfSales = mainPeriodData.CostOfSales,

                    operatingExpenses = mainPeriodData.OperatingExpenses,

                    grossProfit = mainPeriodData.GrossProfit,

                    operatingIncome = mainPeriodData.OperatingIncome,

                    comparison = (object)null

                };



                //add comparison data if requested

                if (!string.IsNullOrEmpty(comparison))

                {

                    var comparisonData = await GetComparisonData(userId, fromDate, toDate, comparison);

                    if (comparisonData != null)

                    {

                        response = new

                        {

                            fromDate = fromDate,

                            toDate = toDate,

                            revenues = mainPeriodData.Revenues,

                            expenses = mainPeriodData.Expenses,

                            totalRevenue = mainPeriodData.TotalRevenue,

                            totalExpenses = mainPeriodData.TotalExpenses,

                            netIncome = mainPeriodData.NetIncome,

                            costOfSales = mainPeriodData.CostOfSales,

                            operatingExpenses = mainPeriodData.OperatingExpenses,

                            grossProfit = mainPeriodData.GrossProfit,

                            operatingIncome = mainPeriodData.OperatingIncome,

                            comparison = comparisonData

                        };

                    }

                }



                _logger.LogInformation($"P&L report generated successfully. Revenue accounts: {mainPeriodData.Revenues?.Count ?? 0}, Expense accounts: {mainPeriodData.Expenses?.Count ?? 0}");



                return Ok(response);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, $"Error generating profit & loss report");

                return StatusCode(500, new { message = "Internal server error while generating profit & loss report", details = ex.Message });

            }

        }



        private async Task<ProfitLossData> GenerateProfitLossData(int userId, DateTime fromDate, DateTime toDate)

        {

            try

            {

                //get all accounts for the user

                var accounts = await _context.ChartOfAccounts

                    .Where(a => a.UserId == userId)

                    .ToListAsync();



                if (!accounts.Any())

                {

                    _logger.LogWarning($"No accounts found for user {userId}");

                    return new ProfitLossData();

                }



                //get all voucher details within the date range

                var voucherDetails = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Include(vd => vd.ChartOfAccount)

                    .Where(vd => vd.VoucherHeader.UserId == userId &&

                                vd.VoucherHeader.VoucherDate >= fromDate &&

                                vd.VoucherHeader.VoucherDate <= toDate)

                    .ToListAsync();



                _logger.LogInformation($"Found {voucherDetails.Count} voucher details for P&L calculation");



                //group by account and calculate balances

                var accountBalances = voucherDetails

                    .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })

                    .Select(g => new

                    {

                        AccountId = g.Key.AccountId,

                        AccountNumber = g.Key.AccountNumber,

                        AccountName = g.Key.AccountName,

                        AccountType = g.Key.AccountType,

                        DebitSum = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount),

                        CreditSum = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)

                    })

                    .ToList();



                //separate revenues and expenses based on account types

                var revenues = new List<object>();

                var expenses = new List<object>();



                decimal totalRevenue = 0;

                decimal totalExpenses = 0;

                decimal costOfSales = 0;

                decimal operatingExpenses = 0;



                foreach (var account in accountBalances)

                {

                    //revenue accounts (typically credit balance accounts)

                    if (IsRevenueAccount(account.AccountType, account.AccountNumber))

                    {

                        var revenueAmount = account.CreditSum - account.DebitSum; //revenue should be positive

                        if (revenueAmount > 0)

                        {

                            revenues.Add(new

                            {

                                accountId = account.AccountId,

                                accountNumber = account.AccountNumber,

                                accountName = account.AccountName,

                                accountType = account.AccountType,

                                amount = revenueAmount

                            });

                            totalRevenue += revenueAmount;

                        }

                    }

                    //expense accounts (typically debit balance accounts)

                    else if (IsExpenseAccount(account.AccountType, account.AccountNumber))

                    {

                        var expenseAmount = account.DebitSum - account.CreditSum; //expense should be positive

                        if (expenseAmount > 0)

                        {

                            expenses.Add(new

                            {

                                accountId = account.AccountId,

                                accountNumber = account.AccountNumber,

                                accountName = account.AccountName,

                                accountType = account.AccountType,

                                amount = expenseAmount

                            });

                            totalExpenses += expenseAmount;



                            //categorize expenses

                            if (IsCostOfSalesAccount(account.AccountNumber, account.AccountName))

                            {

                                costOfSales += expenseAmount;

                            }

                            else if (IsOperatingExpenseAccount(account.AccountNumber, account.AccountName))

                            {

                                operatingExpenses += expenseAmount;

                            }

                        }

                    }

                }



                var netIncome = totalRevenue - totalExpenses;

                var grossProfit = totalRevenue - costOfSales;

                var operatingIncome = grossProfit - operatingExpenses;



                _logger.LogInformation($"P&L calculation completed. Revenue: {totalRevenue:C}, Expenses: {totalExpenses:C}, Net Income: {netIncome:C}");



                return new ProfitLossData

                {

                    Revenues = revenues,

                    Expenses = expenses,

                    TotalRevenue = totalRevenue,

                    TotalExpenses = totalExpenses,

                    NetIncome = netIncome,

                    CostOfSales = costOfSales,

                    OperatingExpenses = operatingExpenses,

                    GrossProfit = grossProfit,

                    OperatingIncome = operatingIncome

                };

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error calculating profit & loss data");

                throw;

            }

        }



        private async Task<object> GetComparisonData(int userId, DateTime fromDate, DateTime toDate, string comparisonType)

        {

            try

            {

                DateTime comparisonFromDate;

                DateTime comparisonToDate;



                switch (comparisonType.ToLower())

                {

                    case "previous-period":

                        var periodLength = (toDate - fromDate).Days;

                        comparisonToDate = fromDate.AddDays(-1);

                        comparisonFromDate = comparisonToDate.AddDays(-periodLength);

                        break;



                    case "previous-year":

                        comparisonFromDate = fromDate.AddYears(-1);

                        comparisonToDate = toDate.AddYears(-1);

                        break;



                    case "budget":

                        //for budget comparison, you would need a budget table

     

                        _logger.LogWarning("Budget comparison not yet implemented");

                        return null;



                    default:

                        _logger.LogWarning($"Unknown comparison type: {comparisonType}");

                        return null;

                }



                var comparisonData = await GenerateProfitLossData(userId, comparisonFromDate, comparisonToDate);



                return new

                {

                    fromDate = comparisonFromDate,

                    toDate = comparisonToDate,

                    totalRevenue = comparisonData.TotalRevenue,

                    totalExpenses = comparisonData.TotalExpenses,

                    netIncome = comparisonData.NetIncome,

                    type = comparisonType

                };

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, $"Error generating comparison data for type: {comparisonType}");

                return null;

            }

        }



        //balance sheet. debits - credits. categorize into assets, liabilities, equity etc.

        [HttpGet("BalanceSheet")]

        public async Task<ActionResult<object>> GetBalanceSheet([FromQuery] DateTime asOfDate)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                var accountBalances = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Include(vd => vd.ChartOfAccount)

                    .Where(vd => vd.VoucherHeader.UserId == userId && vd.VoucherHeader.VoucherDate <= asOfDate)

                    .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })

                    .Select(g => new

                    {

                        accountId = g.Key.AccountId,

                        accountNumber = g.Key.AccountNumber,

                        accountName = g.Key.AccountName,

                        accountType = g.Key.AccountType,

                        debitSum = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount),

                        creditSum = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)

                    })

                    .ToListAsync();



                var assets = new List<object>();

                var liabilities = new List<object>();

                var equity = new List<object>();



                foreach (var account in accountBalances)

                {

                    var balance = account.debitSum - account.creditSum;

                    var amount = Math.Abs(balance);



                    if (amount == 0) continue;



                    if (IsAssetAccount(account.accountType, account.accountNumber))

                    {

                        assets.Add(new

                        {

                            accountId = account.accountId,

                            accountNumber = account.accountNumber,

                            accountName = account.accountName,

                            amount = balance >= 0 ? amount : -amount

                        });

                    }

                    else if (IsLiabilityAccount(account.accountType, account.accountNumber))

                    {

                        liabilities.Add(new

                        {

                            accountId = account.accountId,

                            accountNumber = account.accountNumber,

                            accountName = account.accountName,

                            amount = balance <= 0 ? amount : -amount

                        });

                    }

                    else if (IsEquityAccount(account.accountType, account.accountNumber))

                    {

                        equity.Add(new

                        {

                            accountId = account.accountId,

                            accountNumber = account.accountNumber,

                            accountName = account.accountName,

                            amount = balance <= 0 ? amount : -amount

                        });

                    }

                }



                var totalAssets = assets.Sum(a => (decimal)((dynamic)a).amount);

                var totalLiabilities = liabilities.Sum(l => (decimal)((dynamic)l).amount);

                var totalEquity = equity.Sum(e => (decimal)((dynamic)e).amount);



                return Ok(new

                {

                    assets,

                    liabilities,

                    equity,

                    totalAssets,

                    totalLiabilities,

                    totalEquity,

                    asOfDate

                });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating balance sheet");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        //account ledger report for a specific account

        [HttpGet("AccountLedger/{accountId}")]

        public async Task<ActionResult<object>> GetAccountLedger(

            int accountId,

            [FromQuery] DateTime fromDate,

            [FromQuery] DateTime toDate)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                //date range

                if (fromDate > toDate)

                {

                    return BadRequest(new { message = "From date cannot be later than to date" });

                }



                //get account informatuon

                var account = await _context.ChartOfAccounts

                    .FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);



                if (account == null)

                {

                    return NotFound(new { message = "Account not found" });

                }



                //calculate opening balance

                var openingBalanceDetails = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Where(vd => vd.AccountId == accountId &&

                                vd.VoucherHeader.UserId == userId &&

                                vd.VoucherHeader.VoucherDate < fromDate)

                    .ToListAsync();



                var openingBalance = openingBalanceDetails.Sum(vd => vd.IsDebit ? vd.Amount : -vd.Amount);



                //get entries for the period

                var entries = await _context.VoucherDetails

                    .Include(vd => vd.VoucherHeader)

                    .Where(vd => vd.AccountId == accountId &&

                                vd.VoucherHeader.UserId == userId &&

                                vd.VoucherHeader.VoucherDate >= fromDate &&

                                vd.VoucherHeader.VoucherDate <= toDate)

                    .OrderBy(vd => vd.VoucherHeader.VoucherDate)

                    .ThenBy(vd => vd.VoucherHeader.VoucherId)

                    .Select(vd => new

                    {

                        date = vd.VoucherHeader.VoucherDate,

                        voucherNumber = vd.VoucherHeader.VoucherNumber,

                        description = vd.Description ?? vd.VoucherHeader.Description,

                        debitAmount = vd.IsDebit ? vd.Amount : 0,

                        creditAmount = !vd.IsDebit ? vd.Amount : 0,

                        balance = 0m //ill be calculated

                    })

                    .ToListAsync();



                //calculate running balance

                decimal runningBalance = openingBalance;

                var entriesWithBalance = entries.Select(entry =>

                {

                    runningBalance += entry.debitAmount - entry.creditAmount;

                    return new

                    {

                        entry.date,

                        entry.voucherNumber,

                        entry.description,

                        entry.debitAmount,

                        entry.creditAmount,

                        balance = runningBalance

                    };

                }).ToList();



                var closingBalance = runningBalance;



                return Ok(new

                {

                    accountNumber = account.AccountNumber,

                    accountName = account.AccountName,

                    accountType = account.AccountType,

                    fromDate,

                    toDate,

                    openingBalance,

                    closingBalance,

                    entries = entriesWithBalance

                });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating account ledger");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        //pdf for profit and lsos

        [HttpGet("ExportPDF/profit-loss")]

        public async Task<IActionResult> ExportProfitLossPDF(

            [FromQuery] DateTime fromDate,

            [FromQuery] DateTime toDate,

            [FromQuery] string comparison = null)

        {

            try

            {

                var userId = await GetCurrentUserIdAsync(_context);



                //validate parameters

                if (fromDate > toDate)

                {

                    return BadRequest(new { message = "Invalid date range" });

                }



                //generate P&L data

                var plData = await GenerateProfitLossData(userId, fromDate, toDate);

                var user = await GetCurrentUserAsync(_context);



                //get comparison data if requested

                object comparisonData = null;

                if (!string.IsNullOrEmpty(comparison))

                {

                    comparisonData = await GetComparisonData(userId, fromDate, toDate, comparison);

                }



                //generate PDF

                var pdfBytes = GenerateProfitLossPdfBytes(plData, user, fromDate, toDate, comparisonData);



                var fileName = $"ProfitLoss_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating P&L PDF");

                return StatusCode(500, new { message = "Error generating PDF report" });

            }

        }

        //general ledger
        [HttpGet("ExportPDF/general-ledger")]
        public async Task<IActionResult> ExportGeneralLedgerPDF(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] int? accountId = null)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                if (fromDate > toDate)
                {
                    return BadRequest(new { message = "Invalid date range" });
                }

                //getting general ledger data
                var glData = await GetGeneralLedger(fromDate, toDate, accountId, userId);
                var user = await GetCurrentUserAsync(_context);

                //generate pdf
                var pdfBytes = GenerateGeneralLedgerPdfBytes(glData, user, fromDate, toDate, accountId);

                var fileName = $"GeneralLedger_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating General Ledger PDF");
                return StatusCode(500, new { message = "Error generating PDF report" });
            }
        }

        //trial balance pdf
        [HttpGet("ExportPDF/trial-balance")]
        public async Task<IActionResult> ExportTrialBalancePDF([FromQuery] DateTime asOfDate)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var tbData = await GetTrialBalanceData(asOfDate, userId);
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateTrialBalancePdfBytes(tbData, user, asOfDate);
                var fileName = $"TrialBalance_{asOfDate:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Trial Balance PDF");
                return StatusCode(500, new { message = "Error generating PDF report" });
            }
        }

        //income statement pdf
        [HttpGet("ExportPDF/income-statement")]
        public async Task<IActionResult> ExportIncomeStatementPDF(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                if (fromDate > toDate)
                {
                    return BadRequest(new { message = "Invalid date range" });
                }

                var isData = await GetIncomeStatementData(fromDate, toDate, userId);
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateIncomeStatementPdfBytes(isData, user, fromDate, toDate);
                var fileName = $"IncomeStatement_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Income Statement PDF");
                return StatusCode(500, new { message = "Error generating PDF report" });
            }
        }

        //balance sheet pdf
        [HttpGet("ExportPDF/balance-sheet")]
        public async Task<IActionResult> ExportBalanceSheetPDF([FromQuery] DateTime asOfDate)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var bsData = await GetBalanceSheetData(asOfDate, userId);
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateBalanceSheetPdfBytes(bsData, user, asOfDate);
                var fileName = $"BalanceSheet_{asOfDate:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Balance Sheet PDF");
                return StatusCode(500, new { message = "Error generating PDF report" });
            }
        }

        //account ledger pdf
        [HttpGet("ExportPDF/account-ledger")]
        public async Task<IActionResult> ExportAccountLedgerPDF(
            [FromQuery] int accountId,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                if (fromDate > toDate)
                {
                    return BadRequest(new { message = "Invalid date range" });
                }

                var alData = await GetAccountLedgerData(accountId, fromDate, toDate, userId);
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateAccountLedgerPdfBytes(alData, user, accountId, fromDate, toDate);
                var fileName = $"AccountLedger_{accountId}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Account Ledger PDF");
                return StatusCode(500, new { message = "Error generating PDF report" });
            }
        }

        //helper ,methods for getting data
        private async Task<object> GetGeneralLedger(DateTime fromDate, DateTime toDate, int? accountId, int userId)
        {
            var query = _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Include(vd => vd.ChartOfAccount)
                .Where(vd => vd.VoucherHeader.UserId == userId &&
                            vd.VoucherHeader.VoucherDate >= fromDate &&
                            vd.VoucherHeader.VoucherDate <= toDate);

            if (accountId.HasValue)
            {
                query = query.Where(vd => vd.AccountId == accountId.Value);
            }

            var entries = await query
                .OrderBy(vd => vd.VoucherHeader.VoucherDate)
                .ThenBy(vd => vd.VoucherHeader.VoucherId)
                .Select(vd => new
                {
                    date = vd.VoucherHeader.VoucherDate,
                    voucherNumber = vd.VoucherHeader.VoucherNumber,
                    accountNumber = vd.ChartOfAccount.AccountNumber,
                    accountName = vd.ChartOfAccount.AccountName,
                    description = vd.Description ?? vd.VoucherHeader.Description,
                    debitAmount = vd.IsDebit ? vd.Amount : 0,
                    creditAmount = !vd.IsDebit ? vd.Amount : 0
                })
                .ToListAsync();

            var totalDebits = entries.Sum(e => e.debitAmount);
            var totalCredits = entries.Sum(e => e.creditAmount);

            return new { entries, totalDebits, totalCredits, fromDate, toDate };
        }

        private async Task<object> GetTrialBalanceData(DateTime asOfDate, int userId)
        {
            var accountBalances = await _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Include(vd => vd.ChartOfAccount)
                .Where(vd => vd.VoucherHeader.UserId == userId && vd.VoucherHeader.VoucherDate <= asOfDate)
                .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })
                .Select(g => new
                {
                    accountNumber = g.Key.AccountNumber,
                    accountName = g.Key.AccountName,
                    accountType = g.Key.AccountType,
                    debitBalance = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount) > g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)
                        ? g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount) - g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)
                        : 0,
                    creditBalance = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount) > g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount)
                        ? g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount) - g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount)
                        : 0
                })
                .Where(ab => ab.debitBalance > 0 || ab.creditBalance > 0)
                .OrderBy(ab => ab.accountNumber)
                .ToListAsync();

            var totalDebits = accountBalances.Sum(ab => ab.debitBalance);
            var totalCredits = accountBalances.Sum(ab => ab.creditBalance);

            return new { entries = accountBalances, totalDebits, totalCredits, asOfDate };
        }

        private async Task<object> GetIncomeStatementData(DateTime fromDate, DateTime toDate, int userId)
        {
            var accountBalances = await _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Include(vd => vd.ChartOfAccount)
                .Where(vd => vd.VoucherHeader.UserId == userId &&
                            vd.VoucherHeader.VoucherDate >= fromDate &&
                            vd.VoucherHeader.VoucherDate <= toDate)
                .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })
                .Select(g => new
                {
                    accountId = g.Key.AccountId,
                    accountNumber = g.Key.AccountNumber,
                    accountName = g.Key.AccountName,
                    accountType = g.Key.AccountType,
                    debitSum = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount),
                    creditSum = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)
                })
                .ToListAsync();

            var revenues = new List<object>();
            var expenses = new List<object>();

            foreach (var account in accountBalances)
            {
                if (IsRevenueAccount(account.accountType, account.accountNumber))
                {
                    var amount = account.creditSum - account.debitSum;
                    if (amount > 0)
                    {
                        revenues.Add(new
                        {
                            accountId = account.accountId,
                            accountNumber = account.accountNumber,
                            accountName = account.accountName,
                            amount
                        });
                    }
                }
                else if (IsExpenseAccount(account.accountType, account.accountNumber))
                {
                    var amount = account.debitSum - account.creditSum;
                    if (amount > 0)
                    {
                        expenses.Add(new
                        {
                            accountId = account.accountId,
                            accountNumber = account.accountNumber,
                            accountName = account.accountName,
                            amount
                        });
                    }
                }
            }

            var totalRevenue = revenues.Sum(r => (decimal)((dynamic)r).amount);
            var totalExpenses = expenses.Sum(e => (decimal)((dynamic)e).amount);
            var netIncome = totalRevenue - totalExpenses;

            return new { revenues, expenses, totalRevenue, totalExpenses, netIncome, fromDate, toDate };
        }

        private async Task<object> GetBalanceSheetData(DateTime asOfDate, int userId)
        {
            var accountBalances = await _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Include(vd => vd.ChartOfAccount)
                .Where(vd => vd.VoucherHeader.UserId == userId && vd.VoucherHeader.VoucherDate <= asOfDate)
                .GroupBy(vd => new { vd.AccountId, vd.ChartOfAccount.AccountNumber, vd.ChartOfAccount.AccountName, vd.ChartOfAccount.AccountType })
                .Select(g => new
                {
                    accountId = g.Key.AccountId,
                    accountNumber = g.Key.AccountNumber,
                    accountName = g.Key.AccountName,
                    accountType = g.Key.AccountType,
                    debitSum = g.Where(vd => vd.IsDebit).Sum(vd => vd.Amount),
                    creditSum = g.Where(vd => !vd.IsDebit).Sum(vd => vd.Amount)
                })
                .ToListAsync();

            var assets = new List<object>();
            var liabilities = new List<object>();
            var equity = new List<object>();

            foreach (var account in accountBalances)
            {
                var balance = account.debitSum - account.creditSum;
                var amount = Math.Abs(balance);

                if (amount == 0) continue;

                if (IsAssetAccount(account.accountType, account.accountNumber))
                {
                    assets.Add(new
                    {
                        accountId = account.accountId,
                        accountNumber = account.accountNumber,
                        accountName = account.accountName,
                        amount = balance >= 0 ? amount : -amount
                    });
                }
                else if (IsLiabilityAccount(account.accountType, account.accountNumber))
                {
                    liabilities.Add(new
                    {
                        accountId = account.accountId,
                        accountNumber = account.accountNumber,
                        accountName = account.accountName,
                        amount = balance <= 0 ? amount : -amount
                    });
                }
                else if (IsEquityAccount(account.accountType, account.accountNumber))
                {
                    equity.Add(new
                    {
                        accountId = account.accountId,
                        accountNumber = account.accountNumber,
                        accountName = account.accountName,
                        amount = balance <= 0 ? amount : -amount
                    });
                }
            }

            var totalAssets = assets.Sum(a => (decimal)((dynamic)a).amount);
            var totalLiabilities = liabilities.Sum(l => (decimal)((dynamic)l).amount);
            var totalEquity = equity.Sum(e => (decimal)((dynamic)e).amount);

            return new { assets, liabilities, equity, totalAssets, totalLiabilities, totalEquity, asOfDate };
        }

        private async Task<object> GetAccountLedgerData(int accountId, DateTime fromDate, DateTime toDate, int userId)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.UserId == userId);

            if (account == null) return null;

            var openingBalanceDetails = await _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Where(vd => vd.AccountId == accountId &&
                            vd.VoucherHeader.UserId == userId &&
                            vd.VoucherHeader.VoucherDate < fromDate)
                .ToListAsync();

            var openingBalance = openingBalanceDetails.Sum(vd => vd.IsDebit ? vd.Amount : -vd.Amount);

            var entries = await _context.VoucherDetails
                .Include(vd => vd.VoucherHeader)
                .Where(vd => vd.AccountId == accountId &&
                            vd.VoucherHeader.UserId == userId &&
                            vd.VoucherHeader.VoucherDate >= fromDate &&
                            vd.VoucherHeader.VoucherDate <= toDate)
                .OrderBy(vd => vd.VoucherHeader.VoucherDate)
                .ThenBy(vd => vd.VoucherHeader.VoucherId)
                .Select(vd => new
                {
                    date = vd.VoucherHeader.VoucherDate,
                    voucherNumber = vd.VoucherHeader.VoucherNumber,
                    description = vd.Description ?? vd.VoucherHeader.Description,
                    debitAmount = vd.IsDebit ? vd.Amount : 0,
                    creditAmount = !vd.IsDebit ? vd.Amount : 0
                })
                .ToListAsync();

            var closingBalance = openingBalance + entries.Sum(e => e.debitAmount - e.creditAmount);

            return new
            {
                accountNumber = account.AccountNumber,
                accountName = account.AccountName,
                accountType = account.AccountType,
                fromDate,
                toDate,
                openingBalance,
                closingBalance,
                entries
            };
        }

        //pdf Generation methods
        private byte[] GenerateGeneralLedgerPdfBytes(dynamic glData, User user, DateTime fromDate, DateTime toDate, int? accountId)
        {
            var htmlContent = GenerateGeneralLedgerHtml(glData, user, fromDate, toDate, accountId);
            return ConvertHtmlToPdf(htmlContent);
        }

        private byte[] GenerateTrialBalancePdfBytes(dynamic tbData, User user, DateTime asOfDate)
        {
            var htmlContent = GenerateTrialBalanceHtml(tbData, user, asOfDate);
            return ConvertHtmlToPdf(htmlContent);
        }

        private byte[] GenerateIncomeStatementPdfBytes(dynamic isData, User user, DateTime fromDate, DateTime toDate)
        {
            var htmlContent = GenerateIncomeStatementHtml(isData, user, fromDate, toDate);
            return ConvertHtmlToPdf(htmlContent);
        }

        private byte[] GenerateBalanceSheetPdfBytes(dynamic bsData, User user, DateTime asOfDate)
        {
            var htmlContent = GenerateBalanceSheetHtml(bsData, user, asOfDate);
            return ConvertHtmlToPdf(htmlContent);
        }

        private byte[] GenerateAccountLedgerPdfBytes(dynamic alData, User user, int accountId, DateTime fromDate, DateTime toDate)
        {
            var htmlContent = GenerateAccountLedgerHtml(alData, user, accountId, fromDate, toDate);
            return ConvertHtmlToPdf(htmlContent);
        }

        private byte[] ConvertHtmlToPdf(string htmlContent)
        {
            var converter = new HtmlToPdf();
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 20;
            converter.Options.MarginBottom = 20;
            converter.Options.MarginLeft = 20;
            converter.Options.MarginRight = 20;

            var pdfDocument = converter.ConvertHtmlString(htmlContent);
            var pdfBytes = pdfDocument.Save();
            pdfDocument.Close();

            return pdfBytes;
        }

        //html generation methods
        private string GenerateGeneralLedgerHtml(dynamic glData, User user, DateTime fromDate, DateTime toDate, int? accountId)
        {
            var entries = (IEnumerable<dynamic>)glData.entries;
            var entryRows = string.Join("", entries.Select(e =>
                $"<tr><td>{e.date:MM/dd/yyyy}</td><td>{e.voucherNumber}</td><td>{e.accountNumber} - {e.accountName}</td><td>{e.description}</td><td class=\"amount\">{e.debitAmount:N2}</td><td class=\"amount\">{e.creditAmount:N2}</td></tr>"));

            return GenerateBasePdfHtml("General Ledger", $"Period: {fromDate:MM/dd/yyyy} - {toDate:MM/dd/yyyy}", user,
                $@"<table>
            <thead>
                <tr><th>Date</th><th>Voucher</th><th>Account</th><th>Description</th><th>Debit</th><th>Credit</th></tr>
            </thead>
            <tbody>
                {entryRows}
                <tr class=""total-row"">
                    <td colspan=""4"">Totals</td>
                    <td class=""amount"">{glData.totalDebits:N2}</td>
                    <td class=""amount"">{glData.totalCredits:N2}</td>
                </tr>
            </tbody>
        </table>");
        }

        private string GenerateTrialBalanceHtml(dynamic tbData, User user, DateTime asOfDate)
        {
            var entries = (IEnumerable<dynamic>)tbData.entries;
            var entryRows = string.Join("", entries.Select(e =>
                $"<tr><td>{e.accountNumber}</td><td>{e.accountName}</td><td>{e.accountType}</td><td class=\"amount\">{e.debitBalance:N2}</td><td class=\"amount\">{e.creditBalance:N2}</td></tr>"));

            return GenerateBasePdfHtml("Trial Balance", $"As of: {asOfDate:MM/dd/yyyy}", user,
                $@"<table>
            <thead>
                <tr><th>Account #</th><th>Account Name</th><th>Type</th><th>Debit</th><th>Credit</th></tr>
            </thead>
            <tbody>
                {entryRows}
                <tr class=""total-row"">
                    <td colspan=""3"">Totals</td>
                    <td class=""amount"">{tbData.totalDebits:N2}</td>
                    <td class=""amount"">{tbData.totalCredits:N2}</td>
                </tr>
            </tbody>
        </table>");
        }

        private string GenerateIncomeStatementHtml(dynamic isData, User user, DateTime fromDate, DateTime toDate)
        {
            var revenues = (IEnumerable<dynamic>)isData.revenues;
            var expenses = (IEnumerable<dynamic>)isData.expenses;

            var revenueRows = string.Join("", revenues.Select(r =>
                $"<tr><td>{r.accountNumber}</td><td>{r.accountName}</td><td class=\"amount\">{r.amount:N2}</td></tr>"));

            var expenseRows = string.Join("", expenses.Select(e =>
                $"<tr><td>{e.accountNumber}</td><td>{e.accountName}</td><td class=\"amount\">{e.amount:N2}</td></tr>"));

            return GenerateBasePdfHtml("Income Statement", $"Period: {fromDate:MM/dd/yyyy} - {toDate:MM/dd/yyyy}", user,
                $@"<div class=""section"">
            <h3>Revenues</h3>
            <table>
                <thead><tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr></thead>
                <tbody>
                    {revenueRows}
                    <tr class=""total-row""><td colspan=""2"">Total Revenue</td><td class=""amount"">{isData.totalRevenue:N2}</td></tr>
                </tbody>
            </table>
        </div>
        <div class=""section"">
            <h3>Expenses</h3>
            <table>
                <thead><tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr></thead>
                <tbody>
                    {expenseRows}
                    <tr class=""total-row""><td colspan=""2"">Total Expenses</td><td class=""amount"">{isData.totalExpenses:N2}</td></tr>
                </tbody>
            </table>
        </div>
        <div class=""net-income"">
            <h3>Net Income: {isData.netIncome:N2}</h3>
        </div>");
        }

        private string GenerateBalanceSheetHtml(dynamic bsData, User user, DateTime asOfDate)
        {
            var assets = (IEnumerable<dynamic>)bsData.assets;
            var liabilities = (IEnumerable<dynamic>)bsData.liabilities;
            var equity = (IEnumerable<dynamic>)bsData.equity;

            var assetRows = string.Join("", assets.Select(a =>
                $"<tr><td>{a.accountNumber}</td><td>{a.accountName}</td><td class=\"amount\">{a.amount:N2}</td></tr>"));

            var liabilityRows = string.Join("", liabilities.Select(l =>
                $"<tr><td>{l.accountNumber}</td><td>{l.accountName}</td><td class=\"amount\">{l.amount:N2}</td></tr>"));

            var equityRows = string.Join("", equity.Select(e =>
                $"<tr><td>{e.accountNumber}</td><td>{e.accountName}</td><td class=\"amount\">{e.amount:N2}</td></tr>"));

            return GenerateBasePdfHtml("Balance Sheet", $"As of: {asOfDate:MM/dd/yyyy}", user,
                $@"<div class=""section"">
            <h3>Assets</h3>
            <table>
                <thead><tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr></thead>
                <tbody>
                    {assetRows}
                    <tr class=""total-row""><td colspan=""2"">Total Assets</td><td class=""amount"">{bsData.totalAssets:N2}</td></tr>
                </tbody>
            </table>
        </div>
        <div class=""section"">
            <h3>Liabilities</h3>
            <table>
                <thead><tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr></thead>
                <tbody>
                    {liabilityRows}
                    <tr class=""total-row""><td colspan=""2"">Total Liabilities</td><td class=""amount"">{bsData.totalLiabilities:N2}</td></tr>
                </tbody>
            </table>
        </div>
        <div class=""section"">
            <h3>Equity</h3>
            <table>
                <thead><tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr></thead>
                <tbody>
                    {equityRows}
                    <tr class=""total-row""><td colspan=""2"">Total Equity</td><td class=""amount"">{bsData.totalEquity:N2}</td></tr>
                </tbody>
            </table>
        </div>");
        }

        private string GenerateAccountLedgerHtml(dynamic alData, User user, int accountId, DateTime fromDate, DateTime toDate)
        {
            if (alData == null) return "";

            var entries = (IEnumerable<dynamic>)alData.entries;
            var entryRows = string.Join("", entries.Select(e =>
                $"<tr><td>{e.date:MM/dd/yyyy}</td><td>{e.voucherNumber}</td><td>{e.description}</td><td class=\"amount\">{e.debitAmount:N2}</td><td class=\"amount\">{e.creditAmount:N2}</td></tr>"));

            return GenerateBasePdfHtml("Account Ledger", $"{alData.accountNumber} - {alData.accountName}<br>Period: {fromDate:MM/dd/yyyy} - {toDate:MM/dd/yyyy}", user,
                $@"<div class=""section"">
            <p><strong>Opening Balance:</strong> {alData.openingBalance:N2}</p>
            <p><strong>Closing Balance:</strong> {alData.closingBalance:N2}</p>
        </div>
        <table>
            <thead>
                <tr><th>Date</th><th>Voucher</th><th>Description</th><th>Debit</th><th>Credit</th></tr>
            </thead>
            <tbody>
                {entryRows}
            </tbody>
        </table>");
        }

        private string GenerateBasePdfHtml(string title, string subtitle, User user, string content)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>{title}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; border-bottom: 2px solid #1a237e; padding-bottom: 20px; }}
        .company-name {{ font-size: 24px; font-weight: bold; color: #1a237e; }}
        .report-title {{ font-size: 20px; color: #0d47a1; margin: 10px 0; }}
        .period {{ font-size: 14px; color: #666; }}
        .section {{ margin: 20px 0; }}
        .section h3 {{ color: #1a237e; border-bottom: 1px solid #ccc; padding-bottom: 5px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 10px 0; }}
        th, td {{ padding: 8px 12px; border-bottom: 1px solid #ddd; }}
        th {{ background-color: #f5f5f5; font-weight: bold; }}
        .amount {{ text-align: right; font-family: monospace; }}
        .total-row {{ font-weight: bold; border-top: 2px solid #1a237e; }}
        .net-income {{ background-color: #f0f8ff; font-size: 16px; text-align: center; padding: 10px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <div class=""company-name"">{user?.Name ?? "ClearBooks"} Financial Management</div>
        <div class=""report-title"">{title}</div>
        <div class=""period"">{subtitle}</div>
        <div class=""period"">Generated: {DateTime.Now:MM/dd/yyyy} at {DateTime.Now:HH:mm}</div>
    </div>
    {content}
</body>
</html>";
        }



        private byte[] GenerateProfitLossPdfBytes(ProfitLossData plData, User user, DateTime fromDate, DateTime toDate, object comparisonData)

        {

            try

            {

                var htmlContent = GenerateProfitLossHtml(plData, user, fromDate, toDate, comparisonData);



                var converter = new HtmlToPdf();

                converter.Options.PdfPageSize = PdfPageSize.A4;

                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;

                converter.Options.MarginTop = 20;

                converter.Options.MarginBottom = 20;

                converter.Options.MarginLeft = 20;

                converter.Options.MarginRight = 20;



                var pdfDocument = converter.ConvertHtmlString(htmlContent);

                var pdfBytes = pdfDocument.Save();

                pdfDocument.Close();



                return pdfBytes;

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error generating P&L PDF bytes");

                throw;

            }

        }



        private string GenerateProfitLossHtml(ProfitLossData plData, User user, DateTime fromDate, DateTime toDate, object comparisonData)

        {

            var revenueRows = string.Join("", plData.Revenues.Cast<dynamic>().Select(r =>

                $"<tr><td>{r.accountNumber}</td><td>{r.accountName}</td><td class=\"amount\">{r.amount:N2}</td></tr>"));



            var expenseRows = string.Join("", plData.Expenses.Cast<dynamic>().Select(e =>

                $"<tr><td>{e.accountNumber}</td><td>{e.accountName}</td><td class=\"amount\">{e.amount:N2}</td></tr>"));



            var comparisonSection = comparisonData != null ?

                $@"<div class=""comparison-section"">

                    <h3>Period Comparison</h3>

                    <p>Comparison data would be rendered here</p>

                </div>" : "";



            return $@"

<!DOCTYPE html>

<html>

<head>

    <meta charset=""utf-8"">

    <title>Profit & Loss Statement</title>

    <style>

        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; }}

        .header {{ text-align: center; margin-bottom: 30px; border-bottom: 2px solid #1a237e; padding-bottom: 20px; }}

        .company-name {{ font-size: 24px; font-weight: bold; color: #1a237e; }}

        .report-title {{ font-size: 20px; color: #0d47a1; margin: 10px 0; }}

        .period {{ font-size: 14px; color: #666; }}

        .section {{ margin: 20px 0; }}

        .section h3 {{ color: #1a237e; border-bottom: 1px solid #ccc; padding-bottom: 5px; }}

        table {{ width: 100%; border-collapse: collapse; margin: 10px 0; }}

        th, td {{ padding: 8px 12px; border-bottom: 1px solid #ddd; }}

        th {{ background-color: #f5f5f5; font-weight: bold; }}

        .amount {{ text-align: right; font-family: monospace; }}

        .total-row {{ font-weight: bold; border-top: 2px solid #1a237e; }}

        .net-income {{ background-color: #f0f8ff; font-size: 16px; }}

        .positive {{ color: #27ae60; }}

        .negative {{ color: #e74c3c; }}

    </style>

</head>

<body>

    <div class=""header"">

        <div class=""company-name"">{user?.Name ?? "ClearBooks"} Financial Management</div>

        <div class=""report-title"">Profit & Loss Statement</div>

        <div class=""period"">Period: {fromDate:MMMM dd, yyyy} - {toDate:MMMM dd, yyyy}</div>

        <div class=""period"">Generated: {DateTime.Now:MMMM dd, yyyy} at {DateTime.Now:HH:mm}</div>

    </div>



    <div class=""section"">

        <h3>Revenues</h3>

        <table>

            <thead>

                <tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr>

            </thead>

            <tbody>

                {revenueRows}

                <tr class=""total-row"">

                    <td colspan=""2"">Total Revenue</td>

                    <td class=""amount"">{plData.TotalRevenue:N2}</td>

                </tr>

            </tbody>

        </table>

    </div>



    <div class=""section"">

        <h3>Expenses</h3>

        <table>

            <thead>

                <tr><th>Account #</th><th>Account Name</th><th>Amount</th></tr>

            </thead>

            <tbody>

                {expenseRows}

                <tr class=""total-row"">

                    <td colspan=""2"">Total Expenses</td>

                    <td class=""amount"">{plData.TotalExpenses:N2}</td>

                </tr>

            </tbody>

        </table>

    </div>



    <div class=""section net-income"">

        <h3>Summary</h3>

        <table>

            <tr><td>Total Revenue</td><td class=""amount"">{plData.TotalRevenue:N2}</td></tr>

            <tr><td>Total Expenses</td><td class=""amount"">{plData.TotalExpenses:N2}</td></tr>

            <tr class=""total-row {(plData.NetIncome >= 0 ? "positive" : "negative")}"">

                <td>Net Income</td>

                <td class=""amount"">{plData.NetIncome:N2}</td>

            </tr>

        </table>

    </div>



    {comparisonSection}

</body>

</html>";

        }



        //helpelper methods for account classification

        private bool IsRevenueAccount(string accountType, string accountNumber)

        {

            //rules based on account structure

            return accountType?.ToLower().Contains("revenue") == true ||

                   accountType?.ToLower().Contains("income") == true ||

                   accountType?.ToLower().Contains("sales") == true ||

                   accountNumber?.StartsWith("4") == true || //common revenue account range

                   accountNumber?.StartsWith("30") == true; //alternative revenue range

        }



        private bool IsExpenseAccount(string accountType, string accountNumber)

        {

            // rules based on chart of accounts structure

            return accountType?.ToLower().Contains("expense") == true ||

                   accountType?.ToLower().Contains("cost") == true ||

                   accountNumber?.StartsWith("5") == true || //common expense account range

                   accountNumber?.StartsWith("6") == true || //alternative expense range

                   accountNumber?.StartsWith("7") == true;   //another expense range

        }



        private bool IsAssetAccount(string accountType, string accountNumber)

        {

            return accountType?.ToLower().Contains("asset") == true ||

                   accountNumber?.StartsWith("1") == true;

        }



        private bool IsLiabilityAccount(string accountType, string accountNumber)

        {

            return accountType?.ToLower().Contains("liability") == true ||

                   accountNumber?.StartsWith("2") == true;

        }



        private bool IsEquityAccount(string accountType, string accountNumber)

        {

            return accountType?.ToLower().Contains("equity") == true ||

                   accountNumber?.StartsWith("3") == true;

        }



        private bool IsCostOfSalesAccount(string accountNumber, string accountName)

        {

            var accountNameLower = accountName?.ToLower() ?? "";

            return accountNameLower.Contains("cost of sales") ||

                   accountNameLower.Contains("cost of goods sold") ||

                   accountNameLower.Contains("cogs") ||

                   accountNameLower.Contains("materials") ||

                   accountNameLower.Contains("direct cost") ||

                   accountNumber?.StartsWith("50") == true;

        }



        private bool IsOperatingExpenseAccount(string accountNumber, string accountName)

        {

            var accountNameLower = accountName?.ToLower() ?? "";

            return accountNameLower.Contains("salary") ||

                   accountNameLower.Contains("rent") ||

                   accountNameLower.Contains("utilities") ||

                   accountNameLower.Contains("office") ||

                   accountNameLower.Contains("operating") ||

                   accountNumber?.StartsWith("51") == true ||

                   accountNumber?.StartsWith("52") == true;

        }



        //data transfer class for P&L calculations

        public class ProfitLossData

        {

            public List<object> Revenues { get; set; } = new List<object>();

            public List<object> Expenses { get; set; } = new List<object>();

            public decimal TotalRevenue { get; set; }

            public decimal TotalExpenses { get; set; }

            public decimal NetIncome { get; set; }

            public decimal CostOfSales { get; set; }

            public decimal OperatingExpenses { get; set; }

            public decimal GrossProfit { get; set; }

            public decimal OperatingIncome { get; set; }

        }

    }

}