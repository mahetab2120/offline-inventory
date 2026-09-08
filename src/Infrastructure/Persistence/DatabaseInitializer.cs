using System.Data;
using Dapper;
using Npgsql;

namespace Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    string ConnectionString { get; }
}

public class NpgsqlDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    public IDbConnection CreateConnection()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureDatabaseAndSchemaAsync()
    {
        try
        {
            await EnsureDatabaseCreatedAsync();
            await InitializeSchemaAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Warning initializing PostgreSQL: {ex.Message}");
        }
    }

    private async Task EnsureDatabaseCreatedAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionFactory.ConnectionString);
        string targetDb = builder.Database ?? "offline_inventory_db";

        // Connect to default 'postgres' database to check/create target database
        var masterBuilder = new NpgsqlConnectionStringBuilder(_connectionFactory.ConnectionString)
        {
            Database = "postgres"
        };

        using var conn = new NpgsqlConnection(masterBuilder.ConnectionString);
        await conn.OpenAsync();

        using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @db", conn);
        checkCmd.Parameters.AddWithValue("db", targetDb);
        var exists = await checkCmd.ExecuteScalarAsync() != null;

        if (!exists)
        {
            using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{targetDb}\"", conn);
            await createCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task InitializeSchemaAsync()
    {
        using var conn = _connectionFactory.CreateConnection();

        const string schemaSql = @"
        CREATE TABLE IF NOT EXISTS companies (
            id UUID PRIMARY KEY,
            business_code VARCHAR(50) UNIQUE NOT NULL,
            legal_name VARCHAR(255) NOT NULL,
            trade_name VARCHAR(255),
            gstin VARCHAR(50),
            pan VARCHAR(50),
            business_type INT NOT NULL,
            address TEXT,
            city VARCHAR(100),
            state VARCHAR(100),
            pincode VARCHAR(20),
            contact_phone VARCHAR(50),
            contact_email VARCHAR(100),
            financial_year_start VARCHAR(20) NOT NULL,
            invoice_prefix VARCHAR(20) NOT NULL DEFAULT 'INV-',
            next_invoice_number BIGINT NOT NULL DEFAULT 1,
            currency_symbol VARCHAR(10) NOT NULL DEFAULT '₹',
            is_initialized BOOLEAN NOT NULL DEFAULT FALSE,
            created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            initialized_at_utc TIMESTAMP WITH TIME ZONE
        );

        CREATE TABLE IF NOT EXISTS users (
            id UUID PRIMARY KEY,
            username VARCHAR(100) UNIQUE NOT NULL,
            password_hash TEXT NOT NULL,
            salt TEXT NOT NULL,
            full_name VARCHAR(255) NOT NULL,
            email VARCHAR(255),
            phone VARCHAR(50),
            role INT NOT NULL,
            permissions BIGINT NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            last_login_at_utc TIMESTAMP WITH TIME ZONE
        );

        CREATE TABLE IF NOT EXISTS licenses (
            id UUID PRIMARY KEY,
            business_code VARCHAR(50) NOT NULL,
            license_key VARCHAR(255) NOT NULL,
            plan INT NOT NULL,
            issued_date_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            expiry_date_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            grace_period_days INT NOT NULL DEFAULT 7,
            max_users INT NOT NULL DEFAULT 5,
            max_branches INT NOT NULL DEFAULT 1,
            max_products INT NOT NULL DEFAULT 10000,
            enabled_modules_json TEXT NOT NULL,
            status INT NOT NULL,
            signature TEXT NOT NULL,
            last_verified_utc TIMESTAMP WITH TIME ZONE NOT NULL
        );

        CREATE TABLE IF NOT EXISTS audit_logs (
            id BIGSERIAL PRIMARY KEY,
            timestamp_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            user_id UUID,
            username VARCHAR(100) NOT NULL,
            machine_name VARCHAR(100) NOT NULL,
            module VARCHAR(100) NOT NULL,
            action INT NOT NULL,
            record_id VARCHAR(100) NOT NULL,
            old_value TEXT,
            new_value TEXT,
            reason TEXT
        );

        CREATE TABLE IF NOT EXISTS categories (
            id UUID PRIMARY KEY,
            name VARCHAR(150) NOT NULL,
            code VARCHAR(50) UNIQUE NOT NULL,
            description TEXT,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
        );

        CREATE TABLE IF NOT EXISTS rack_locations (
            id UUID PRIMARY KEY,
            warehouse VARCHAR(100) NOT NULL,
            section VARCHAR(50) NOT NULL,
            rack VARCHAR(50) NOT NULL,
            shelf VARCHAR(50) NOT NULL,
            bin VARCHAR(50) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS products (
            id UUID PRIMARY KEY,
            name VARCHAR(255) NOT NULL,
            sku VARCHAR(100) UNIQUE NOT NULL,
            barcode VARCHAR(100),
            hsn_code VARCHAR(50),
            category_id UUID REFERENCES categories(id) ON DELETE SET NULL,
            category_name VARCHAR(150) NOT NULL DEFAULT 'General',
            unit VARCHAR(20) NOT NULL DEFAULT 'PCS',
            purchase_price NUMERIC(15, 2) NOT NULL DEFAULT 0,
            selling_price NUMERIC(15, 2) NOT NULL DEFAULT 0,
            mrp NUMERIC(15, 2) NOT NULL DEFAULT 0,
            gst_rate NUMERIC(5, 2) NOT NULL DEFAULT 18.00,
            cess_rate NUMERIC(5, 2) NOT NULL DEFAULT 0.00,
            current_stock NUMERIC(15, 2) NOT NULL DEFAULT 0,
            min_stock_alert NUMERIC(15, 2) NOT NULL DEFAULT 5,
            rack_location_id UUID REFERENCES rack_locations(id) ON DELETE SET NULL,
            rack_location VARCHAR(150) NOT NULL DEFAULT 'Shelf A-01',
            batch_number VARCHAR(100),
            expiry_date TIMESTAMP WITH TIME ZONE,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
        );

        CREATE TABLE IF NOT EXISTS invoices (
            id UUID PRIMARY KEY,
            invoice_number VARCHAR(50) UNIQUE NOT NULL,
            invoice_date_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            customer_id UUID,
            customer_name VARCHAR(255) NOT NULL DEFAULT 'Walk-in Customer',
            customer_phone VARCHAR(50),
            customer_gstin VARCHAR(50),
            sub_total NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_discount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            taxable_amount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_cgst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_sgst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_igst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_cess NUMERIC(15, 2) NOT NULL DEFAULT 0,
            round_off NUMERIC(15, 2) NOT NULL DEFAULT 0,
            grand_total NUMERIC(15, 2) NOT NULL DEFAULT 0,
            payment_method INT NOT NULL DEFAULT 1,
            amount_paid NUMERIC(15, 2) NOT NULL DEFAULT 0,
            change_due NUMERIC(15, 2) NOT NULL DEFAULT 0,
            status INT NOT NULL DEFAULT 2,
            cashier_user_id UUID,
            cashier_username VARCHAR(100) NOT NULL,
            notes TEXT
        );

        CREATE TABLE IF NOT EXISTS invoice_items (
            id UUID PRIMARY KEY,
            invoice_id UUID REFERENCES invoices(id) ON DELETE CASCADE,
            product_id UUID REFERENCES products(id) ON DELETE SET NULL,
            product_name VARCHAR(255) NOT NULL,
            sku VARCHAR(100) NOT NULL,
            hsn_code VARCHAR(50),
            quantity NUMERIC(15, 2) NOT NULL DEFAULT 1,
            unit VARCHAR(20) NOT NULL DEFAULT 'PCS',
            unit_price NUMERIC(15, 2) NOT NULL DEFAULT 0,
            discount_amount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            taxable_value NUMERIC(15, 2) NOT NULL DEFAULT 0,
            gst_rate NUMERIC(5, 2) NOT NULL DEFAULT 18.00,
            cgst_amount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            sgst_amount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            igst_amount NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_amount NUMERIC(15, 2) NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS decrypted_audit_packages (
            id UUID PRIMARY KEY,
            business_code VARCHAR(50) NOT NULL,
            legal_name VARCHAR(255) NOT NULL,
            gstin VARCHAR(50),
            accounting_period VARCHAR(100) NOT NULL,
            export_timestamp_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            decrypted_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
            total_invoices INT NOT NULL DEFAULT 0,
            total_revenue NUMERIC(15, 2) NOT NULL DEFAULT 0,
            taxable_turnover NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_cgst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_sgst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_igst NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_tax NUMERIC(15, 2) NOT NULL DEFAULT 0,
            total_products INT NOT NULL DEFAULT 0,
            inventory_valuation NUMERIC(15, 2) NOT NULL DEFAULT 0,
            audit_logs_count INT NOT NULL DEFAULT 0,
            package_file_path TEXT,
            raw_payload_json TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);
        CREATE INDEX IF NOT EXISTS idx_products_sku ON products(sku);
        CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs(timestamp_utc);
        CREATE INDEX IF NOT EXISTS idx_invoices_date ON invoices(invoice_date_utc);
        CREATE INDEX IF NOT EXISTS idx_decrypted_audit_business ON decrypted_audit_packages(business_code);
        CREATE INDEX IF NOT EXISTS idx_decrypted_audit_date ON decrypted_audit_packages(decrypted_at_utc);

        ALTER TABLE products ADD COLUMN IF NOT EXISTS category_name VARCHAR(150) NOT NULL DEFAULT 'General';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS rack_location VARCHAR(150) NOT NULL DEFAULT 'Shelf A-01';
        ";

        await conn.ExecuteAsync(schemaSql);
    }
}
