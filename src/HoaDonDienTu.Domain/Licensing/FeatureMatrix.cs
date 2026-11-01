using HoaDonDienTu.Domain.Common;

namespace HoaDonDienTu.Domain.Licensing
{
    /// <summary>Ma trận tính năng theo chế độ, DD-03 mục 3.3. Nguồn duy nhất cho UI lẫn application service.</summary>
    public static class FeatureMatrix
    {
        public static bool IsAllowed(AppMode mode, AppFeature feature)
        {
            switch (mode)
            {
                case AppMode.Standard:
                    return feature != AppFeature.MultiCompany;
                case AppMode.Service:
                    return true;
                default:
                    return feature == AppFeature.ReadExport;
            }
        }
    }
}
