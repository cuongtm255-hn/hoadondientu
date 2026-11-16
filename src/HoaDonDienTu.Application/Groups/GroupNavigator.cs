using System;
using System.Collections.Generic;

namespace HoaDonDienTu.Application.Groups
{
    /// <summary>
    /// Điều phối hai nhóm chức năng: tạo shell lười, đổi nhóm mà giữ phiên nhóm kia, nhớ nhóm gần nhất (DD-DVC-00).
    /// </summary>
    public sealed class GroupNavigator : IDisposable
    {
        private readonly IGroupShellFactory _factory;
        private readonly IGroupPreferenceStore _store;
        private readonly Dictionary<AppGroup, IGroupShell> _shells = new Dictionary<AppGroup, IGroupShell>();

        public GroupNavigator(IGroupShellFactory factory, IGroupPreferenceStore store)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            if (store == null) throw new ArgumentNullException("store");
            _factory = factory;
            _store = store;
        }

        /// <summary>Nhóm đang hiển thị, null nếu chưa mở nhóm nào.</summary>
        public AppGroup? Active { get; private set; }

        /// <summary>Nhóm chọn sẵn trên màn chọn: nhóm gần nhất, mặc định Hóa đơn.</summary>
        public AppGroup SuggestedGroup
        {
            get { return _store.GetLastGroup() ?? AppGroup.Invoice; }
        }

        public bool IsCreated(AppGroup group)
        {
            return _shells.ContainsKey(group);
        }

        /// <summary>Mở nhóm lần đầu từ màn chọn khi khởi động.</summary>
        public void Open(AppGroup group)
        {
            Activate(group);
        }

        /// <summary>
        /// Đổi nhóm. Nếu nhóm hiện tại có tác vụ chạy thì gọi <paramref name="confirmLeave"/> với mô tả; từ chối thì ở lại và trả false.
        /// </summary>
        public bool Switch(AppGroup group, Func<string, bool> confirmLeave)
        {
            if (Active == group) return true;

            if (Active.HasValue)
            {
                IGroupShell current = _shells[Active.Value];
                string reason;
                if (!current.CanLeave(out reason))
                {
                    if (confirmLeave == null || !confirmLeave(reason)) return false;
                    current.StopRunningTasks();
                }
                current.Hide();
            }

            Activate(group);
            return true;
        }

        private void Activate(AppGroup group)
        {
            IGroupShell shell;
            if (!_shells.TryGetValue(group, out shell))
            {
                shell = _factory.Create(group);
                _shells[group] = shell;
            }
            _store.SetLastGroup(group);
            Active = group;
            shell.Show();
        }

        public void Dispose()
        {
            foreach (IGroupShell shell in _shells.Values) shell.Dispose();
            _shells.Clear();
            Active = null;
        }
    }
}
