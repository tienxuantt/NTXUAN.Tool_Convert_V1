using QLTS.Tool_Khao_Sat.BL;
using QLTS.Tool_Khao_Sat.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QLTS.Tool_Khao_Sat
{
    public partial class fForm : Form
    {
        // Api thao tác với dữ liệu
        private HttpApi api = null;

        // Biến cờ thông báo có khảo sát tiếp k
        private bool upgradeActive = true;

        public string scriptExecute = "";

        // Các tỉnh trên chương trình
        private List<Tenant> tenants_v1 = new List<Tenant>();
        private List<Tenant> tenants_v2 = new List<Tenant>();

        private List<Tenant> tenantsUpgrade = new List<Tenant>();

        private int TotalTenantUpgrade = 0;
        private int TotalTenantUpgradeSuccess = 0;

        private int TotalTenantUpgradeDone = 0;
        private int TotalTenantUpgradeFail = 0;

        // Số thread đang thực hiện
        private int countProcess = 0;
        // Tối đa bao nhiêu thread
        private int maxProcess = 15;

        private object key = new object();

        public fForm()
        {
            InitializeComponent();
            InitForm();
        }

        public void InitForm()
        {
            api = new HttpApi();

            LoadCookie();
        }

        private void LoadCookie()
        {
            CookieJson result = new CookieJson();

            result = api.LoadCookieJson();

            txtCookie.Text = result.Cookie;

            api.CookieValue = result.Cookie;
        }

        // Lưu lại cookie
        private void btnSaveCookie_Click(object sender, EventArgs e)
        {
            var cookie = txtCookie.Text;

            if (string.IsNullOrEmpty(cookie))
            {
                MessageBox.Show("Vui lòng điền cookie", "Thông báo");
            }
            else
            {
                api.CookieValue = cookie;

                api.SaveCookieJson(cookie);
            }
        }

        // Binding vào listview1
        private void BindingListView1()
        {
            listView1.Items.Clear();

            foreach (var item in tenants_v1)
            {
                ListViewItem lsvItem = new ListViewItem("");

                lsvItem.SubItems.Add(item.stt.ToString());
                lsvItem.SubItems.Add(item.tenant_name);
                lsvItem.SubItems.Add(item.tenant_code);
                lsvItem.SubItems.Add(item.error);

                lsvItem.Tag = item;

                listView1.Items.Add(lsvItem);
            }
        }

        private async Task GetListTenantV1()
        {
            int STT = 1;

            List<Tenant> result = new List<Tenant>();

            tenants_v1 = new List<Tenant>();

            List<string> tenantCodes = api.ReadFileTxt("TenantCodeV1.txt");

            result = await api.GetTeants_v1();

            foreach (var item in tenantCodes)
            {
                var tenantFocus = result.FirstOrDefault(s => s.tenant_code.Equals(item));

                if(tenantFocus != null)
                {
                    tenantFocus.stt = STT++;
                    tenants_v1.Add(tenantFocus);
                }
            }
        }

        private async Task GetListTenantV2()
        {
            int STT = 1;

            List<Tenant> result = new List<Tenant>();

            tenants_v2 = new List<Tenant>();

            List<string> tenantCodes = api.ReadFileTxt("TenantCodeV2.txt");

            result = await api.GetTeants_v2();

            foreach (var item in tenantCodes)
            {
                var tenantFocus = result.FirstOrDefault(s => s.tenant_code.Equals(item));

                if (tenantFocus != null)
                {
                    tenantFocus.stt = STT++;
                    tenants_v2.Add(tenantFocus);
                }
            }
        }

        // Validate cookie
        private bool ValidateForm(bool isValidateScript = false)
        {
            bool valid = true;

            if (string.IsNullOrEmpty(api.CookieValue))
            {
                valid = false;

                MessageBox.Show("Vui lòng nhập cookie", "Thông báo");
            }

            if (isValidateScript && valid && string.IsNullOrEmpty(scriptExecute))
            {
                valid = false;

                MessageBox.Show("Vui lòng sửa script", "Thông báo");
            }

            return valid;
        }

        private async void btnLoadTeant_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateForm())
                {
                    return;
                }

                await GetListTenantV1();
                await GetListTenantV2();

                BindingListView1();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Có lỗi xảy ra");
            }
        }

        // Bắt đầu khảo sát các subject được chọn
        private void StartUpgrade()
        {
            // Lấy ra các subject khảo sát
            tenantsUpgrade = GetListTenantUpgrade();

            // Tổng vấn đề khảo sát
            TotalTenantUpgrade = tenantsUpgrade.Count;
            // Reset vấn đề
            TotalTenantUpgradeSuccess = 0;

            TotalTenantUpgradeDone = 0;
            TotalTenantUpgradeFail = 0;

            // Bật cờ
            upgradeActive = true;

            // Tạo một luồng riêng cho khảo sát
            Thread thread = new Thread(async () => {
                StartUpgradeBackground();
            });

            thread.IsBackground = true;
            thread.Start();
        }

        // Bắt đầu khảo sát
        private void StartUpgradeBackground()
        {
            while (upgradeActive && TotalTenantUpgradeSuccess < TotalTenantUpgrade)
            {
                Thread.Sleep(700);

                if (countProcess < maxProcess)
                {
                    var tenant = tenantsUpgrade.FirstOrDefault(s => !s.survey_success);

                    if (tenant != null)
                    {
                        tenant.survey_success = true;
                        tenant.error = "";

                        NewProcessExecuteScript(tenant);
                    }
                }
            }

            MessageBox.Show("Đã convert xong", "Thông báo");
        }

        // Cập nhật lại số tỉnh, số đơn vị lỗi
        private void UpdateListViewItem(Tenant tenant, bool isStart = false)
        {
            var listItem = listView1.CheckedItems;

            if (listItem.Count > 0)
            {
                for(var i = 0; i< listItem.Count; i++)
                {
                    if(listItem[i].SubItems[3].Text == tenant.tenant_code)
                    {
                        listItem[i].UseItemStyleForSubItems = false;
                        listItem[i].SubItems[2].ForeColor = Color.Blue;
                        listItem[i].SubItems[3].ForeColor = Color.Blue;

                        listItem[i].Tag = tenant;

                        if (isStart)
                        {
                            listItem[i].SubItems[4].Text = "Đang convert...";
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(tenant.error))
                            {
                                listItem[i].UseItemStyleForSubItems = false;
                                listItem[i].SubItems[4].ForeColor = Color.Red;
                                listItem[i].SubItems[4].Text = tenant.error;
                                listItem[i].SubItems[4].Font = new Font(listView1.Font, FontStyle.Bold);
                            }
                            else
                            {
                                listItem[i].UseItemStyleForSubItems = false;
                                listItem[i].SubItems[4].ForeColor = Color.Blue;
                                listItem[i].SubItems[4].Text = "Done";
                                listItem[i].SubItems[4].Font = new Font(listView1.Font, FontStyle.Bold);
                            }
                        }

                        break;
                    }
                }
            }
        }

        // Lấy các bản ghi được chọn
        private List<Tenant> GetListTenantUpgrade()
        {
            List<Tenant> listResult = new List<Tenant>();

            var listItem = listView1.CheckedItems;

            if(listItem.Count > 0)
            {
                foreach (ListViewItem item in listItem)
                {
                    Tenant tenant = item.Tag as Tenant;

                    tenant.survey_success = false;
                    tenant.total_record = 0;
                    tenant.error = "";

                    listResult.Add(tenant);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một bản ghi", "Thông báo");
            }

            return listResult;
        }

        private void WriteLog(string log)
        {
            string pathLog = "D:/LogKhaoSat.txt";

            try
            {
                StreamWriter sw = new StreamWriter(pathLog, true);

                sw.Write(log);
                sw.Close();
            }
            catch
            {
            }
        }

        private void NewProcessExecuteScript(Tenant tenant)
        {
            listView1.Invoke(new MethodInvoker(() =>
            {
                UpdateListViewItem(tenant, true);
            }));

            lock (key)
            {
                countProcess++;
            }

            Thread thread = new Thread(async () => {
                var scriptv2 = new List<DataV1>();

                try
                {
                    scriptv2 = await api.ExecuteScript_v1(tenant.tenant_id.ToString(), scriptExecute);
                }
                catch (Exception ex)
                {
                    tenant.error = ex.Message;
                    TotalTenantUpgradeFail++;
                }

                // Nếu không có lỗi
                if (string.IsNullOrEmpty(tenant.error))
                {
                    try
                    {
                        var tenantV2 = tenants_v2.FirstOrDefault(s => s.stt == tenant.stt);

                        foreach (var scriptItem in scriptv2)
                        {
                            var result = await api.ExecuteScript_v2(tenantV2.tenant_id.ToString(), scriptItem.Data);
                        }

                        TotalTenantUpgradeDone++;
                    }
                    catch (Exception ex)
                    {
                        tenant.error = ex.Message;
                        TotalTenantUpgradeFail++;
                    }
                }

                TotalTenantUpgradeSuccess++;

                labelTenantProcess.Invoke(new MethodInvoker(() =>
                {
                    labelTenantProcess.Text = string.Format("{0}/{1}", TotalTenantUpgradeSuccess, TotalTenantUpgrade);
                }));

                labelSuccess.Invoke(new MethodInvoker(() =>
                {
                    labelSuccess.Text = string.Format("Success: {0}", TotalTenantUpgradeDone);
                }));

                labelFail.Invoke(new MethodInvoker(() =>
                {
                    labelFail.Text = string.Format("Fail: {0}", TotalTenantUpgradeFail);
                }));

                listView1.Invoke(new MethodInvoker(() =>
                {
                    UpdateListViewItem(tenant);
                }));

                lock (key)
                {
                    countProcess--;
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            upgradeActive = false;
        }

        private void btnUpgrade_Click(object sender, EventArgs e)
        {
            upgradeActive = true;

            if (!ValidateForm(true))
            {
                return;
            }

            StartUpgrade();
        }

        private void btnEditScript_Click(object sender, EventArgs e)
        {
            var formScript = new fFormScript(this);

            formScript.ShowDialog();
        }

        private void checkBoxAll_CheckedChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.Items)
            {
                item.Checked = checkBoxAll.Checked;
            }
        }
    }
}
