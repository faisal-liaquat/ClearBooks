## 📚 ClearBooks

> **Lightweight Web-Based Accounting System for Small Businesses**

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Core-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2019-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap)](https://getbootstrap.com/)

---

## 🎯 Overview

**ClearBooks** is a user-friendly, web-based accounting and ledger management system specifically designed for small and micro-enterprises (SMEs) in emerging markets. Built as a Final Year Project, ClearBooks addresses the critical gap between manual bookkeeping and expensive enterprise ERP solutions.

Unlike commercial platforms that require ongoing subscriptions and steep learning curves, ClearBooks emphasizes **simplicity**, **affordability**, and **accessibility** — empowering shopkeepers, freelancers, and home-based entrepreneurs to transition confidently from manual to digital bookkeeping.

<img width="1901" height="857" alt="Screenshot 2025-07-01 153149" src="https://github.com/user-attachments/assets/ee5aa0fd-391e-465b-a962-147751d3437f" />

---
## ✨ Key Features

### Core Accounting Modules
- **📊 Chart of Accounts** — Hierarchical account structure with unlimited parent-child nesting
- **📝 Voucher Entry** — Double-entry bookkeeping with automatic debit/credit validation
- **💰 Payments & Receipts** — Comprehensive cash flow management with attachment support
- **🔗 General Ledger Mapping** — Automated debit/credit rules for repetitive transactions
- **📈 Financial Reports** — Generate Trial Balance, P&L Statement, Balance Sheet, and Account Ledgers

### Technical Highlights
- **Multi-tenant Architecture** — Row-level data isolation for secure multi-user environments
- **PDF Export** — Print-ready financial statements and vouchers for compliance
- **Responsive Design** — Mobile-friendly interface built with Bootstrap 5
- **Audit Trails** — Comprehensive timestamping and transaction history
- **Referential Integrity** — Foreign key constraints prevent data inconsistencies

---
## 🏗️ System Architecture

ClearBooks follows a **three-tier architecture** for maintainability and scalability:
```
┌─────────────────────────────────────────┐
│      Presentation Layer (UI)           │
│   HTML5 • CSS3 • Bootstrap 5 • JS      │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│    Business Logic Layer (API)          │
│      ASP.NET Core • C# • MVC           │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│        Data Layer (Database)           │
│   SQL Server 2019 • EF Core • Stored   │
│              Procedures                 │
└─────────────────────────────────────────┘
```
### Database Design Principles
- **Normalization (3NF)** — Eliminates redundancy, ensures data integrity
- **Soft Deletes** — Maintains audit trails without data loss
- **Cascading Constraints** — Prevents orphaned records
- **Performance Indexing** — Optimized for fast queries on large datasets

---

## 🛠️ Technology Stack

| Layer | Technologies |
|-------|-------------|
| **Frontend** | HTML5, CSS3, JavaScript, Bootstrap 5, jQuery |
| **Backend** | ASP.NET Core, C#, Entity Framework Core |
| **Database** | Microsoft SQL Server 2019/2022 |
| **PDF Generation** | iText 7 |
| **File Storage** | Azure Blob Storage (optional) |
| **Version Control** | Git, GitHub |

---

## 📦 Installation

### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download) or higher
- [SQL Server 2019+](https://www.microsoft.com/sql-server/sql-server-downloads) or SQL Server Express
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or VS Code

### Setup Instructions

1. **Clone the repository**
```bash
   git clone https://github.com/faisal-liaquat/ClearBooks.git
   cd ClearBooks
```

2. **Configure Database Connection**
   
   Update `appsettings.json` with your SQL Server connection string:
```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=ClearBooks;Trusted_Connection=True;"
     }
   }
```

3. **Apply Database Migrations**
```bash
   dotnet ef database update
```

4. **Build and Run**
```bash
   dotnet restore
   dotnet build
   dotnet run
```

5. **Access the Application**
   
   Open your browser and navigate to: `https://localhost:5001`

---
## 📸 Screenshots

### Dashboard
<img width="1896" height="860" alt="Screenshot 2025-07-01 153245" src="https://github.com/user-attachments/assets/b0472ec9-1011-4ee1-b10f-d2a29ded1598" />

### Voucher Entry
<img width="1485" height="770" alt="Screenshot 2025-07-01 153528" src="https://github.com/user-attachments/assets/00d1f963-fe38-449f-992b-a6e7fd22512a" />

### Financial Reports
<img width="1541" height="790" alt="Screenshot 2025-07-01 154003" src="https://github.com/user-attachments/assets/2aea0bec-38d1-4a64-9ad3-2f76a6e90f8b" />
<img width="1550" height="767" alt="Screenshot 2025-07-01 154025" src="https://github.com/user-attachments/assets/0228ac18-ccb6-4c66-9efe-0a12abbee9c8" />
<img width="1534" height="762" alt="Screenshot 2025-07-01 154038" src="https://github.com/user-attachments/assets/20c4ecf4-3c0a-496d-83ca-48f4d636de1d" />


---

## 🧪 Testing

The system was validated through:
- ✅ Supervisor walkthrough testing covering all major workflows
- ✅ Live validation of double-entry bookkeeping rules (ΣDebit = ΣCredit)
- ✅ Foreign key integrity testing
- ✅ Multi-user isolation verification

**Note:** Automated unit testing is planned for future iterations.

---

## 🌍 Impact & SDG Alignment

ClearBooks contributes to the United Nations **Sustainable Development Goals**:

| SDG | Target | How ClearBooks Helps |
|-----|--------|---------------------|
| **SDG 8** | Decent Work & Economic Growth | Enables SME formalization and improves credit-readiness |
| **SDG 9** | Industry, Innovation & Infrastructure | Provides accessible ICT solutions for underserved communities |
| **SDG 16** | Peace, Justice & Strong Institutions | Promotes financial transparency and regulatory compliance |

---

## 🚀 Future Enhancements

### Planned Features
- [ ] **Automated Testing Suite** — Unit tests with xUnit, UI tests with Selenium
- [ ] **Multi-Currency Support** — Foreign exchange rate management
- [ ] **Urdu Localization** — Bilingual interface for broader accessibility
- [ ] **Mobile App** — Flutter-based Android/iOS companion
- [ ] **Advanced Analytics** — AI-driven forecasting and anomaly detection
- [ ] **POS Integration** — Sync with third-party point-of-sale systems
- [ ] **Inventory Management** — Expand to micro-ERP capabilities
- [ ] **Tax Automation** — FBR-compliant return generation for Pakistan

### Known Limitations
- No automated test coverage (manual testing only)
- Single-language interface (English only)
- Limited performance benchmarking
- No offline-first capability

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📞 Contact

For questions or collaboration:

**Muhammad Faisal**  
📧 Email: [muhammedfaisal1423@gmail.com](mailto:muhammedfaisal1423@gmail.com)  
🔗 GitHub: [@faisal-liaquat](https://github.com/faisal-liaquat)

---

---

<div align="center">

**Built with ❤️ for Small Businesses**

⭐ If you find this project useful, please consider giving it a star!

</div>
