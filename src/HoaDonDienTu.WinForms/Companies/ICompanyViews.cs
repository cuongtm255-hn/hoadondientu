using System;
using System.Collections.Generic;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.WinForms.Companies
{
    public enum CompanyRowAction
    {
        Work,
        Open,
        Detail,
        OpenFolder,
        Rename,
        Unlink
    }

    /// <summary>Một dòng của SCR-10 đã định dạng sẵn để view chỉ việc hiển thị.</summary>
    public sealed class CompanyRowViewModel
    {
        public CompanyId Id { get; set; }
        public string Name { get; set; }
        public string StoragePath { get; set; }
        public string TaxCode { get; set; }
        public string LastOpened { get; set; }
        public string InvoiceCount { get; set; }
        public string Size { get; set; }
        public string StatusText { get; set; }
        public CompanyCatalogStatus Status { get; set; }
        public bool IsCurrent { get; set; }
        public CompanyRowAction PrimaryAction { get; set; }
        public string PrimaryActionText { get; set; }
    }

    public sealed class CompanyListStats
    {
        public int Total { get; set; }
        public int NeedAttention { get; set; }
        public string TotalSize { get; set; }
        public string CurrentTaxCode { get; set; }
        public string CurrentName { get; set; }
        public int Shown { get; set; }
    }

    public interface ICompanyListView
    {
        event EventHandler SearchRequested;
        event EventHandler AddRequested;
        event EventHandler<CompanyRowActionEventArgs> RowActionRequested;

        string SearchText { get; }

        /// <summary>Null hoặc rỗng nghĩa là mọi trạng thái; ngược lại là "Active" hoặc "Attention".</summary>
        string StatusFilter { get; }

        void ShowBanner(string text, bool readOnly);
        void ShowAddEnabled(bool enabled);
        void ShowRows(IList<CompanyRowViewModel> rows, CompanyListStats stats);
    }

    public sealed class CompanyRowActionEventArgs : EventArgs
    {
        public CompanyRowActionEventArgs(CompanyId id, CompanyRowAction action)
        {
            Id = id;
            Action = action;
        }

        public CompanyId Id { get; private set; }
        public CompanyRowAction Action { get; private set; }
    }

    public sealed class CompanyEditConfig
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string PrimaryText { get; set; }
        public bool ShowBreadcrumb { get; set; }
        public bool ShowCancel { get; set; }
        public bool TaxCodeReadOnly { get; set; }
        public bool FolderReadOnly { get; set; }
        public IList<string> AfterSteps { get; set; }
    }

    public interface ICompanyEditView
    {
        event EventHandler InputChanged;
        event EventHandler TaxCodeEdited;
        event EventHandler BrowseRequested;
        event EventHandler PrimaryRequested;
        event EventHandler CancelRequested;

        CompanyInput ReadInput();
        void Configure(CompanyEditConfig config);
        void ShowValues(string taxCode, string displayName, string storagePath);
        void SetStoragePath(string path);
        void ShowChecks(IList<CompanyCheck> checks);
        void SetPrimaryEnabled(bool enabled);
        void ShowMessage(string message);
    }
}
