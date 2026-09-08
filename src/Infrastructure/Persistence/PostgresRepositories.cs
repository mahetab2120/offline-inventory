using System.Data;
using Application.Interfaces;
using Dapper;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Persistence;

public class PostgresCompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresCompanyRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Company?> GetCompanyAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, business_code AS BusinessCode, legal_name AS LegalName, trade_name AS TradeName,
                   gstin AS GSTIN, pan AS PAN, business_type AS BusinessType, address, city, state,
                   pincode, contact_phone AS ContactPhone, contact_email AS ContactEmail,
                   financial_year_start AS FinancialYearStart, invoice_prefix AS InvoicePrefix,
                   next_invoice_number AS NextInvoiceNumber, currency_symbol AS CurrencySymbol,
                   is_initialized AS IsInitialized, created_at_utc AS CreatedAtUtc,
                   initialized_at_utc AS InitializedAtUtc
            FROM companies
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<Company>(sql);
    }

    public async Task<bool> SaveCompanyAsync(Company company)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO companies (
                id, business_code, legal_name, trade_name, gstin, pan, business_type,
                address, city, state, pincode, contact_phone, contact_email,
                financial_year_start, invoice_prefix, next_invoice_number, currency_symbol,
                is_initialized, created_at_utc, initialized_at_utc
            ) VALUES (
                @Id, @BusinessCode, @LegalName, @TradeName, @GSTIN, @PAN, @BusinessType,
                @Address, @City, @State, @Pincode, @ContactPhone, @ContactEmail,
                @FinancialYearStart, @InvoicePrefix, @NextInvoiceNumber, @CurrencySymbol,
                @IsInitialized, @CreatedAtUtc, @InitializedAtUtc
            )
            ON CONFLICT (business_code) DO UPDATE SET
                legal_name = EXCLUDED.legal_name,
                trade_name = EXCLUDED.trade_name,
                gstin = EXCLUDED.gstin,
                pan = EXCLUDED.pan,
                business_type = EXCLUDED.business_type,
                address = EXCLUDED.address,
                city = EXCLUDED.city,
                state = EXCLUDED.state,
                pincode = EXCLUDED.pincode,
                contact_phone = EXCLUDED.contact_phone,
                contact_email = EXCLUDED.contact_email,
                is_initialized = EXCLUDED.is_initialized,
                initialized_at_utc = EXCLUDED.initialized_at_utc;";
        
        int rows = await conn.ExecuteAsync(sql, company);
        return rows > 0;
    }

    public async Task<bool> IsCompanyInitializedAsync()
    {
        var company = await GetCompanyAsync();
        return company != null && company.IsInitialized;
    }
}

public class PostgresUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresUserRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, username, password_hash AS PasswordHash, salt, full_name AS FullName,
                   email, phone, role, permissions, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc, last_login_at_utc AS LastLoginAtUtc
            FROM users
            WHERE LOWER(username) = LOWER(@Username)
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, username, password_hash AS PasswordHash, salt, full_name AS FullName,
                   email, phone, role, permissions, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc, last_login_at_utc AS LastLoginAtUtc
            FROM users
            WHERE id = @Id;";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, username, password_hash AS PasswordHash, salt, full_name AS FullName,
                   email, phone, role, permissions, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc, last_login_at_utc AS LastLoginAtUtc
            FROM users;";
        return await conn.QueryAsync<User>(sql);
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO users (
                id, username, password_hash, salt, full_name, email, phone,
                role, permissions, is_active, created_at_utc
            ) VALUES (
                @Id, @Username, @PasswordHash, @Salt, @FullName, @Email, @Phone,
                @Role, @Permissions, @IsActive, @CreatedAtUtc
            )
            ON CONFLICT (username) DO UPDATE SET
                password_hash = EXCLUDED.password_hash,
                salt = EXCLUDED.salt,
                full_name = EXCLUDED.full_name,
                role = EXCLUDED.role,
                permissions = EXCLUDED.permissions,
                is_active = EXCLUDED.is_active;";
        int rows = await conn.ExecuteAsync(sql, user);
        return rows > 0;
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE users SET
                full_name = @FullName,
                email = @Email,
                phone = @Phone,
                role = @Role,
                permissions = @Permissions,
                is_active = @IsActive
            WHERE id = @Id;";
        int rows = await conn.ExecuteAsync(sql, user);
        return rows > 0;
    }

    public async Task<bool> UpdateLastLoginAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE users SET last_login_at_utc = @Now WHERE id = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Now = DateTime.UtcNow, Id = userId });
        return rows > 0;
    }
}

public class PostgresLicenseRepository : ILicenseRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresLicenseRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LicenseRecord?> GetCurrentLicenseAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, business_code AS BusinessCode, license_key AS LicenseKey, plan,
                   issued_date_utc AS IssuedDateUtc, expiry_date_utc AS ExpiryDateUtc,
                   grace_period_days AS GracePeriodDays, max_users AS MaxUsers,
                   max_branches AS MaxBranches, max_products AS MaxProducts,
                   enabled_modules_json AS EnabledModulesJson, status, signature,
                   last_verified_utc AS LastVerifiedUtc
            FROM licenses
            ORDER BY issued_date_utc DESC
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<LicenseRecord>(sql);
    }

    public async Task<bool> SaveLicenseAsync(LicenseRecord license)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO licenses (
                id, business_code, license_key, plan, issued_date_utc, expiry_date_utc,
                grace_period_days, max_users, max_branches, max_products,
                enabled_modules_json, status, signature, last_verified_utc
            ) VALUES (
                @Id, @BusinessCode, @LicenseKey, @Plan, @IssuedDateUtc, @ExpiryDateUtc,
                @GracePeriodDays, @MaxUsers, @MaxBranches, @MaxProducts,
                @EnabledModulesJson, @Status, @Signature, @LastVerifiedUtc
            );";
        int rows = await conn.ExecuteAsync(sql, license);
        return rows > 0;
    }
}

public class PostgresAuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresAuditLogRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task LogAsync(AuditLog log)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO audit_logs (
                timestamp_utc, user_id, username, machine_name, module, action, record_id, old_value, new_value, reason
            ) VALUES (
                @TimestampUtc, @UserId, @Username, @MachineName, @Module, @Action, @RecordId, @OldValue, @NewValue, @Reason
            );";
        await conn.ExecuteAsync(sql, log);
    }

    public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 100)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, timestamp_utc AS TimestampUtc, user_id AS UserId, username,
                   machine_name AS MachineName, module, action, record_id AS RecordId,
                   old_value AS OldValue, new_value AS NewValue, reason
            FROM audit_logs
            ORDER BY timestamp_utc DESC
            LIMIT @Count;";
        return await conn.QueryAsync<AuditLog>(sql, new { Count = count });
    }
}

public class PostgresProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresProductRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, name, sku, barcode, hsn_code AS HSNCode, category_id AS CategoryId,
                   category_name AS CategoryName, unit, purchase_price AS PurchasePrice,
                   selling_price AS SellingPrice, mrp AS MRP, gst_rate AS GSTRate,
                   cess_rate AS CessRate, current_stock AS CurrentStock, min_stock_alert AS MinStockAlert,
                   rack_location_id AS RackLocationId, rack_location AS RackLocation,
                   batch_number AS BatchNumber, expiry_date AS ExpiryDate, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc
            FROM products
            WHERE id = @Id;";
        return await conn.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
    }

    public async Task<Product?> GetByBarcodeOrSkuAsync(string code)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, name, sku, barcode, hsn_code AS HSNCode, category_id AS CategoryId,
                   category_name AS CategoryName, unit, purchase_price AS PurchasePrice,
                   selling_price AS SellingPrice, mrp AS MRP, gst_rate AS GSTRate,
                   cess_rate AS CessRate, current_stock AS CurrentStock, min_stock_alert AS MinStockAlert,
                   rack_location_id AS RackLocationId, rack_location AS RackLocation,
                   batch_number AS BatchNumber, expiry_date AS ExpiryDate, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc
            FROM products
            WHERE LOWER(barcode) = LOWER(@Code) OR LOWER(sku) = LOWER(@Code)
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<Product>(sql, new { Code = code });
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, name, sku, barcode, hsn_code AS HSNCode, category_id AS CategoryId,
                   category_name AS CategoryName, unit, purchase_price AS PurchasePrice,
                   selling_price AS SellingPrice, mrp AS MRP, gst_rate AS GSTRate,
                   cess_rate AS CessRate, current_stock AS CurrentStock, min_stock_alert AS MinStockAlert,
                   rack_location_id AS RackLocationId, rack_location AS RackLocation,
                   batch_number AS BatchNumber, expiry_date AS ExpiryDate, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc
            FROM products
            WHERE is_active = TRUE
            ORDER BY name;";
        return await conn.QueryAsync<Product>(sql);
    }

    public async Task<IEnumerable<Product>> GetLowStockProductsAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, name, sku, barcode, hsn_code AS HSNCode, category_id AS CategoryId,
                   category_name AS CategoryName, unit, purchase_price AS PurchasePrice,
                   selling_price AS SellingPrice, mrp AS MRP, gst_rate AS GSTRate,
                   cess_rate AS CessRate, current_stock AS CurrentStock, min_stock_alert AS MinStockAlert,
                   rack_location_id AS RackLocationId, rack_location AS RackLocation,
                   batch_number AS BatchNumber, expiry_date AS ExpiryDate, is_active AS IsActive,
                   created_at_utc AS CreatedAtUtc
            FROM products
            WHERE is_active = TRUE AND current_stock <= min_stock_alert
            ORDER BY current_stock ASC;";
        return await conn.QueryAsync<Product>(sql);
    }

    public async Task<bool> CreateAsync(Product product)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO products (
                id, name, sku, barcode, hsn_code, category_id, category_name, unit, purchase_price,
                selling_price, mrp, gst_rate, cess_rate, current_stock, min_stock_alert,
                rack_location_id, rack_location, batch_number, expiry_date, is_active, created_at_utc
            ) VALUES (
                @Id, @Name, @SKU, @Barcode, @HSNCode, @CategoryId, @CategoryName, @Unit, @PurchasePrice,
                @SellingPrice, @MRP, @GSTRate, @CessRate, @CurrentStock, @MinStockAlert,
                @RackLocationId, @RackLocation, @BatchNumber, @ExpiryDate, @IsActive, @CreatedAtUtc
            )
            ON CONFLICT (sku) DO UPDATE SET
                name = EXCLUDED.name,
                barcode = EXCLUDED.barcode,
                hsn_code = EXCLUDED.hsn_code,
                category_name = EXCLUDED.category_name,
                selling_price = EXCLUDED.selling_price,
                mrp = EXCLUDED.mrp,
                gst_rate = EXCLUDED.gst_rate,
                current_stock = EXCLUDED.current_stock,
                rack_location = EXCLUDED.rack_location,
                batch_number = EXCLUDED.batch_number,
                expiry_date = EXCLUDED.expiry_date;";
        int rows = await conn.ExecuteAsync(sql, product);
        return rows > 0;
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE products SET
                name = @Name,
                barcode = @Barcode,
                hsn_code = @HSNCode,
                category_name = @CategoryName,
                rack_location = @RackLocation,
                batch_number = @BatchNumber,
                expiry_date = @ExpiryDate,
                selling_price = @SellingPrice,
                mrp = @MRP,
                gst_rate = @GSTRate,
                current_stock = @CurrentStock,
                min_stock_alert = @MinStockAlert
            WHERE id = @Id;";
        int rows = await conn.ExecuteAsync(sql, product);
        return rows > 0;
    }

    public async Task<bool> AdjustStockAsync(Guid productId, decimal quantityChange)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE products SET
                current_stock = current_stock + @Qty
            WHERE id = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Qty = quantityChange, Id = productId });
        return rows > 0;
    }
}

public class PostgresInvoiceRepository : IInvoiceRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresInvoiceRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, invoice_number AS InvoiceNumber, invoice_date_utc AS InvoiceDateUtc,
                   customer_id AS CustomerId, customer_name AS CustomerName,
                   customer_phone AS CustomerPhone, customer_gstin AS CustomerGSTIN,
                   sub_total AS SubTotal, total_discount AS TotalDiscount,
                   taxable_amount AS TaxableAmount, total_cgst AS TotalCGST,
                   total_sgst AS TotalSGST, total_igst AS TotalIGST, total_cess AS TotalCess,
                   round_off AS RoundOff, grand_total AS GrandTotal, payment_method AS PaymentMethod,
                   amount_paid AS AmountPaid, change_due AS ChangeDue, status,
                   cashier_user_id AS CashierUserId, cashier_username AS CashierUsername, notes
            FROM invoices
            WHERE id = @Id;";
        
        var invoice = await conn.QueryFirstOrDefaultAsync<Invoice>(sql, new { Id = id });
        if (invoice != null)
        {
            const string itemSql = @"
                SELECT id, invoice_id AS InvoiceId, product_id AS ProductId,
                       product_name AS ProductName, sku, hsn_code AS HSNCode,
                       quantity, unit, unit_price AS UnitPrice, discount_amount AS DiscountAmount,
                       taxable_value AS TaxableValue, gst_rate AS GSTRate,
                       cgst_amount AS CGSTAmount, sgst_amount AS SGSTAmount,
                       igst_amount AS IGSTAmount, total_amount AS TotalAmount
                FROM invoice_items
                WHERE invoice_id = @InvoiceId;";
            var items = await conn.QueryAsync<InvoiceItem>(itemSql, new { InvoiceId = id });
            invoice.Items = items.ToList();
        }
        return invoice;
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, invoice_number AS InvoiceNumber, invoice_date_utc AS InvoiceDateUtc,
                   customer_id AS CustomerId, customer_name AS CustomerName,
                   customer_phone AS CustomerPhone, customer_gstin AS CustomerGSTIN,
                   sub_total AS SubTotal, total_discount AS TotalDiscount,
                   taxable_amount AS TaxableAmount, total_cgst AS TotalCGST,
                   total_sgst AS TotalSGST, total_igst AS TotalIGST, total_cess AS TotalCess,
                   round_off AS RoundOff, grand_total AS GrandTotal, payment_method AS PaymentMethod,
                   amount_paid AS AmountPaid, change_due AS ChangeDue, status,
                   cashier_user_id AS CashierUserId, cashier_username AS CashierUsername, notes
            FROM invoices
            WHERE LOWER(invoice_number) = LOWER(@InvoiceNumber);";
        return await conn.QueryFirstOrDefaultAsync<Invoice>(sql, new { InvoiceNumber = invoiceNumber });
    }

    public async Task<IEnumerable<Invoice>> GetRecentInvoicesAsync(int limit = 50)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, invoice_number AS InvoiceNumber, invoice_date_utc AS InvoiceDateUtc,
                   customer_id AS CustomerId, customer_name AS CustomerName,
                   customer_phone AS CustomerPhone, customer_gstin AS CustomerGSTIN,
                   sub_total AS SubTotal, total_discount AS TotalDiscount,
                   taxable_amount AS TaxableAmount, total_cgst AS TotalCGST,
                   total_sgst AS TotalSGST, total_igst AS TotalIGST, total_cess AS TotalCess,
                   round_off AS RoundOff, grand_total AS GrandTotal, payment_method AS PaymentMethod,
                   amount_paid AS AmountPaid, change_due AS ChangeDue, status,
                   cashier_user_id AS CashierUserId, cashier_username AS CashierUsername, notes
            FROM invoices
            ORDER BY invoice_date_utc DESC
            LIMIT @Limit;";
        return await conn.QueryAsync<Invoice>(sql, new { Limit = limit });
    }

    public async Task<string> GetNextInvoiceNumberAsync(string prefix)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(MAX(next_invoice_number), 1)
            FROM companies;";
        long nextNum = await conn.ExecuteScalarAsync<long>(sql);
        if (nextNum == 0) nextNum = 1;

        // Increment in company table
        await conn.ExecuteAsync("UPDATE companies SET next_invoice_number = next_invoice_number + 1;");

        return $"{prefix}{nextNum:D6}";
    }

    public async Task<bool> SaveInvoiceAtomicAsync(Invoice invoice)
    {
        using var conn = _factory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var trans = conn.BeginTransaction();
        try
        {
            const string invSql = @"
                INSERT INTO invoices (
                    id, invoice_number, invoice_date_utc, customer_id, customer_name,
                    customer_phone, customer_gstin, sub_total, total_discount,
                    taxable_amount, total_cgst, total_sgst, total_igst, total_cess,
                    round_off, grand_total, payment_method, amount_paid, change_due,
                    status, cashier_user_id, cashier_username, notes
                ) VALUES (
                    @Id, @InvoiceNumber, @InvoiceDateUtc, @CustomerId, @CustomerName,
                    @CustomerPhone, @CustomerGSTIN, @SubTotal, @TotalDiscount,
                    @TaxableAmount, @TotalCGST, @TotalSGST, @TotalIGST, @TotalCess,
                    @RoundOff, @GrandTotal, @PaymentMethod, @AmountPaid, @ChangeDue,
                    @Status, @CashierUserId, @CashierUsername, @Notes
                )
                ON CONFLICT (invoice_number) DO UPDATE SET
                    invoice_date_utc = EXCLUDED.invoice_date_utc,
                    customer_id = EXCLUDED.customer_id,
                    customer_name = EXCLUDED.customer_name,
                    customer_phone = EXCLUDED.customer_phone,
                    customer_gstin = EXCLUDED.customer_gstin,
                    sub_total = EXCLUDED.sub_total,
                    total_discount = EXCLUDED.total_discount,
                    taxable_amount = EXCLUDED.taxable_amount,
                    total_cgst = EXCLUDED.total_cgst,
                    total_sgst = EXCLUDED.total_sgst,
                    total_igst = EXCLUDED.total_igst,
                    total_cess = EXCLUDED.total_cess,
                    round_off = EXCLUDED.round_off,
                    grand_total = EXCLUDED.grand_total,
                    payment_method = EXCLUDED.payment_method,
                    amount_paid = EXCLUDED.amount_paid,
                    change_due = EXCLUDED.change_due,
                    status = EXCLUDED.status,
                    cashier_user_id = EXCLUDED.cashier_user_id,
                    cashier_username = EXCLUDED.cashier_username,
                    notes = EXCLUDED.notes;";

            await conn.ExecuteAsync(invSql, invoice, trans);

            if (invoice.Items != null && invoice.Items.Count > 0)
            {
                // Delete existing line items for idempotency during re-import
                await conn.ExecuteAsync("DELETE FROM invoice_items WHERE invoice_id = @InvoiceId;", new { InvoiceId = invoice.Id }, trans);

                const string itemSql = @"
                    INSERT INTO invoice_items (
                        id, invoice_id, product_id, product_name, sku, hsn_code,
                        quantity, unit, unit_price, discount_amount, taxable_value,
                        gst_rate, cgst_amount, sgst_amount, igst_amount, total_amount
                    ) VALUES (
                        @Id, @InvoiceId, @ProductId, @ProductName, @SKU, @HSNCode,
                        @Quantity, @Unit, @UnitPrice, @DiscountAmount, @TaxableValue,
                        @GSTRate, @CGSTAmount, @SGSTAmount, @IGSTAmount, @TotalAmount
                    );";

                foreach (var item in invoice.Items)
                {
                    item.InvoiceId = invoice.Id;
                    if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
                    await conn.ExecuteAsync(itemSql, item, trans);
                }
            }

            trans.Commit();
            return true;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }
}

public class PostgresDecryptedAuditPackageRepository : IDecryptedAuditPackageRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresDecryptedAuditPackageRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> SaveDecryptedPackageAsync(DecryptedAuditPackage package)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO decrypted_audit_packages (
                id, business_code, legal_name, gstin, accounting_period,
                export_timestamp_utc, decrypted_at_utc, total_invoices, total_revenue,
                taxable_turnover, total_cgst, total_sgst, total_igst, total_tax,
                total_products, inventory_valuation, audit_logs_count, package_file_path, raw_payload_json
            ) VALUES (
                @Id, @BusinessCode, @LegalName, @GSTIN, @AccountingPeriod,
                @ExportTimestampUtc, @DecryptedAtUtc, @TotalInvoices, @TotalRevenue,
                @TaxableTurnover, @TotalCGST, @TotalSGST, @TotalIGST, @TotalTax,
                @TotalProducts, @InventoryValuation, @AuditLogsCount, @PackageFilePath, @RawPayloadJson
            )
            ON CONFLICT (id) DO UPDATE SET
                business_code = EXCLUDED.business_code,
                legal_name = EXCLUDED.legal_name,
                gstin = EXCLUDED.gstin,
                accounting_period = EXCLUDED.accounting_period,
                export_timestamp_utc = EXCLUDED.export_timestamp_utc,
                decrypted_at_utc = EXCLUDED.decrypted_at_utc,
                total_invoices = EXCLUDED.total_invoices,
                total_revenue = EXCLUDED.total_revenue,
                taxable_turnover = EXCLUDED.taxable_turnover,
                total_cgst = EXCLUDED.total_cgst,
                total_sgst = EXCLUDED.total_sgst,
                total_igst = EXCLUDED.total_igst,
                total_tax = EXCLUDED.total_tax,
                total_products = EXCLUDED.total_products,
                inventory_valuation = EXCLUDED.inventory_valuation,
                audit_logs_count = EXCLUDED.audit_logs_count,
                package_file_path = EXCLUDED.package_file_path,
                raw_payload_json = EXCLUDED.raw_payload_json;";
        int rows = await conn.ExecuteAsync(sql, package);
        return rows > 0;
    }

    public async Task<IEnumerable<DecryptedAuditPackage>> GetAllDecryptedPackagesAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT id, business_code AS BusinessCode, legal_name AS LegalName, gstin AS GSTIN,
                   accounting_period AS AccountingPeriod, export_timestamp_utc AS ExportTimestampUtc,
                   decrypted_at_utc AS DecryptedAtUtc, total_invoices AS TotalInvoices,
                   total_revenue AS TotalRevenue, taxable_turnover AS TaxableTurnover,
                   total_cgst AS TotalCGST, total_sgst AS TotalSGST, total_igst AS TotalIGST,
                   total_tax AS TotalTax, total_products AS TotalProducts,
                   inventory_valuation AS InventoryValuation, audit_logs_count AS AuditLogsCount,
                   package_file_path AS PackageFilePath, raw_payload_json AS RawPayloadJson
            FROM decrypted_audit_packages
            ORDER BY decrypted_at_utc DESC;";
        return await conn.QueryAsync<DecryptedAuditPackage>(sql);
    }
}
