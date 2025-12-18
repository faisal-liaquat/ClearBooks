using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClearBooksFYP.Models
{
    public class ClearBooksDbContext : DbContext
    {
        public ClearBooksDbContext(DbContextOptions<ClearBooksDbContext> options)
            : base(options)
        {
        }

        //authentication tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        //existing tables
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<GLMapping> GLMappings { get; set; }
        public DbSet<VoucherHeader> VoucherHeaders { get; set; }
        public DbSet<VoucherDetail> VoucherDetails { get; set; }
        public DbSet<PaymentHeader> PaymentHeaders { get; set; }
        public DbSet<PaymentDetail> PaymentDetails { get; set; }
        public DbSet<PaymentAttachment> PaymentAttachments { get; set; }
        public DbSet<Receipt> Receipts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //user relationships
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            //session relationships
            modelBuilder.Entity<UserSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            //chart of Accounts user relationship
            modelBuilder.Entity<ChartOfAccount>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //gL Mapping user relationship
            modelBuilder.Entity<GLMapping>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //gL Mapping relationships
            modelBuilder.Entity<GLMapping>()
                .HasOne<ChartOfAccount>()
                .WithMany()
                .HasForeignKey(m => m.DebitAccount)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GLMapping>()
                .HasOne<ChartOfAccount>()
                .WithMany()
                .HasForeignKey(m => m.CreditAccount)
                .OnDelete(DeleteBehavior.Restrict);

            //voucher user relationship
            modelBuilder.Entity<VoucherHeader>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //voucher relationships
            modelBuilder.Entity<VoucherHeader>()
                .HasMany(vh => vh.VoucherDetails)
                .WithOne(vd => vd.VoucherHeader)
                .HasForeignKey(vd => vd.VoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoucherDetail>()
                .HasOne(vd => vd.VoucherHeader)
                .WithMany(vh => vh.VoucherDetails)
                .HasForeignKey(vd => vd.VoucherId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            modelBuilder.Entity<VoucherDetail>()
                .HasOne(vd => vd.ChartOfAccount)
                .WithMany()
                .HasForeignKey(vd => vd.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            //payment user relationship
            modelBuilder.Entity<PaymentHeader>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //payment relationships
            modelBuilder.Entity<PaymentHeader>()
                .HasMany(ph => ph.PaymentDetails)
                .WithOne(pd => pd.Payment)
                .HasForeignKey(pd => pd.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentHeader>()
                .HasMany(ph => ph.PaymentAttachments)
                .WithOne(pa => pa.Payment)
                .HasForeignKey(pa => pa.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentHeader>()
                .HasOne(ph => ph.ChartOfAccount)
                .WithMany()
                .HasForeignKey(ph => ph.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            modelBuilder.Entity<PaymentDetail>()
                .HasOne(pd => pd.VoucherHeader)
                .WithMany()
                .HasForeignKey(pd => pd.VoucherId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            //receipt user relationship
            modelBuilder.Entity<Receipt>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //add RemainingAmount as a non-mapped property
            modelBuilder.Entity<VoucherHeader>()
                .Ignore(v => v.RemainingAmount);
        }
    }

    //authentication Models
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [Column("username")]
        [MaxLength(50)]
        public string Username { get; set; }

        [Required]
        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; }

        [Required]
        [Column("password_hash")]
        [MaxLength(255)]
        public string PasswordHash { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    [Table("user_sessions")]
    public class UserSession
    {
        [Key]
        [Column("session_id")]
        [MaxLength(255)]
        public string SessionId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        //navigation property
        public virtual User User { get; set; }
    }

    //updated existing models with UserId
    [Table("chart_of_accounts")]
    public class ChartOfAccount
    {
        [Key]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("account_number")]
        [Required]
        public string AccountNumber { get; set; }

        [Column("account_name")]
        [Required]
        public string AccountName { get; set; }

        [Column("account_type")]
        [Required]
        public string AccountType { get; set; }

        [Column("subaccount")]
        public string? Subaccount { get; set; }

        [Column("parent_account")]
        public int? ParentAccount { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("gl_mappings")]
    public class GLMapping
    {
        [Key]
        [Column("mapping_id")]
        public int MappingId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("transaction_type")]
        public string TransactionType { get; set; }

        [Required]
        [Column("debit_account")]
        public int DebitAccount { get; set; }

        [Required]
        [Column("credit_account")]
        public int CreditAccount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("voucher_header")]
    public class VoucherHeader
    {
        [Key]
        [Column("voucher_id")]
        public int VoucherId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("voucher_number")]
        public string VoucherNumber { get; set; }

        [Required]
        [Column("voucher_date")]
        public DateTime VoucherDate { get; set; }

        [Required]
        [Column("transaction_type")]
        public string TransactionType { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<VoucherDetail> VoucherDetails { get; set; }

        [NotMapped]
        public decimal RemainingAmount { get; set; }
    }

    [Table("voucher_detail")]
    public class VoucherDetail
    {
        [Key]
        [Column("detail_id")]
        public int DetailId { get; set; }

        [Required]
        [Column("voucher_id")]
        public int VoucherId { get; set; }

        [Required]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Required]
        [Column("is_debit")]
        public bool IsDebit { get; set; }

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public virtual VoucherHeader? VoucherHeader { get; set; }
        public virtual ChartOfAccount? ChartOfAccount { get; set; }
    }

    [Table("payment_header")]
    public class PaymentHeader
    {
        [Key]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("payment_number")]
        public string PaymentNumber { get; set; }

        [Required]
        [Column("payment_date")]
        public DateTime PaymentDate { get; set; }

        [Required]
        [Column("payee_name")]
        public string PayeeName { get; set; }

        [Required]
        [Column("payment_method")]
        public string PaymentMethod { get; set; }

        [Required]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("reference_number")]
        public string ReferenceNumber { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Draft";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public virtual ChartOfAccount ChartOfAccount { get; set; }
        public virtual ICollection<PaymentDetail> PaymentDetails { get; set; }
        public virtual ICollection<PaymentAttachment> PaymentAttachments { get; set; }
    }

    [Table("payment_detail")]
    public class PaymentDetail
    {
        [Key]
        [Column("detail_id")]
        public int DetailId { get; set; }

        [Required]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Required]
        [Column("voucher_id")]
        public int VoucherId { get; set; }

        [Required]
        [Column("amount_paid")]
        public decimal AmountPaid { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public virtual PaymentHeader Payment { get; set; }
        public virtual VoucherHeader VoucherHeader { get; set; }
    }

    [Table("payment_attachments")]
    public class PaymentAttachment
    {
        [Key]
        [Column("attachment_id")]
        public int AttachmentId { get; set; }

        [Required]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Required]
        [Column("file_name")]
        public string FileName { get; set; }

        [Required]
        [Column("file_path")]
        public string FilePath { get; set; }

        [Required]
        [Column("file_type")]
        public string FileType { get; set; }

        [Required]
        [Column("upload_date")]
        public DateTime UploadDate { get; set; }

        // Navigation property
        public virtual PaymentHeader Payment { get; set; }
    }

    [Table("receipts1")]
    public class Receipt
    {
        [Key]
        [Column("receipt_id")]
        public int ReceiptId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("receipt_number")]
        public string ReceiptNumber { get; set; }

        [Required]
        [Column("payer_name")]
        public string PayerName { get; set; }

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Required]
        [Column("currency")]
        public string Currency { get; set; }

        [Required]
        [Column("date")]
        public DateTime Date { get; set; }

        [Required]
        [Column("payment_method")]
        public string PaymentMethod { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}