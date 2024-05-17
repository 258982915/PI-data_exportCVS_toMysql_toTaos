using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using PISDK;
using PISDKCommon;
using PISDKDlg;
using RestSharp;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;

namespace PI_Edit
{
    public partial class Form1 : Form
    {
        PISDK.PISDK piSDK;   // 定义PISDK接口piSDK
        Server server;       // 定义Server接口server 
        PIPoint pt;    //定义PI Point
        int totalrow;

        private PISDK.ListData listData;
        private PointList pilist = new PointList();
        bool scroll = false;

        private bool stopFlag = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            piSDK = new PISDK.PISDK();  // 创建PISDKClass对象，并使接口piSDK指向它

            //对ListBox控件comboBox1进行初始化，使其列出服务器列表中的所有服务器名
            foreach (Server srv in piSDK.Servers)
            {
                comboBox1.Items.Add(srv.Name);
            }

            //// 使comboBox1控件的选中项为默认服务器名
            comboBox1.SelectedItem = piSDK.Servers.DefaultServer.Name;

            // 使接口server指向默认服务器
            server = piSDK.Servers[comboBox1.SelectedItem.ToString()];
        }



        /// <summary>
        /// 建立与PI服务器之间的连接，有3种连接PI服务器的方式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // 如果已经连接到PI服务器，则先断开与PI服务器之间的连接
                if (server.Connected)
                {
                    server.Close();
                }
                string servername = comboBox1.SelectedItem.ToString();

                server = piSDK.Servers[servername];//此除更换成实际的PI服务器地址
                if (!string.IsNullOrEmpty(textBox4.Text.ToString()))
                    server.DefaultUser = textBox4.Text.ToString();

                if (!server.Connected)
                {
                    server.Open();
                }
                richTextBox1_S("\n" + server.Name + " Connect Success!");

            }
            catch (Exception ex)
            {
                MessageBox.Show("Can not connect to PI Server.\r\nDetail is: " + ex.Message);
                return;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text.ToString()))
            {
                MessageBox.Show("tag textbox is Empty , please enter tag");
                return;
            }
            string output = string.Empty;
            pt = server.PIPoints[textBox1.Text.ToString()]; //SINUSOID
            StringBuilder csvContent = new StringBuilder();
            var day = Convert.ToInt16(textBox3.Text.ToString());

            //output = "\n---------------------------------------------- Export single tag to CVS files ----------------------------------------------";
            output = "\n #region " + DateTime.Now.ToUniversalTime() + " Export single tag to CVS files \n";
            richTextBox1_S(output);

            var insertString = "";
            var MysqlinsertString = "";
            var pv = "";
            var count = 0;
            var total = 0;

            var pitime = "";
            var pioriTime = "";
            int repeatCount = 0;

            if (pt.PointType.ToString().IndexOf("Digital") > -1)
            {
                richTextBox1_S("\n PointType:" + pt.PointType.ToString() + " not Underway");
            }
            else
            {
                PIValues piValues = pt.Data.RecordedValues(DateTime.Now.AddDays(-day).ToString(), DateTime.Now.ToString(), BoundaryTypeConstants.btInside);
                foreach (PIValue piValue in piValues)
                {
                    PITimeServer.PITime piTime = piValue.TimeStamp;

                    pv = piValue.Value.ToString();
                    if (piValue.Value.GetType().IsCOMObject)
                        pv = ((DigitalState)piValue.Value).Name.ToString();




                    pitime = piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss.fff");


                    if (pioriTime == pitime)
                    {
                        repeatCount++;
                        pitime = piTime.LocalDate.AddMilliseconds(repeatCount).ToString("yyyy-MM-dd HH:mm:ss.fff");

                    }
                    else
                    {
                        repeatCount = 0;
                        pioriTime = pitime;
                    }

                    csvContent.AppendLine("'" + pitime + "'," + pv);

                    insertString += "('" + pitime + "', '" + pv + "'),";
                    MysqlinsertString += "('" + pt.Name + "', '" + pitime + "', '" + pv + "'),";

                    count++;
                    if (count == 1000)
                    {
                        richTextBox1_S("\n count:" + count);

                        sendPOSTBatch(pt.Name, insertString.Substring(0, insertString.Length - 1), MysqlinsertString.Substring(0, MysqlinsertString.Length - 1));
                        insertString = "";
                        MysqlinsertString = "";
                        count = 0;

                    }
                    total++;

                }

                if (count > 0)
                {
                    richTextBox1_S("\n count:" + count);
                    // make sure to remove the trailing comma
                    sendPOSTBatch(pt.Name, insertString.Substring(0, insertString.Length - 1), MysqlinsertString.Substring(0, MysqlinsertString.Length - 1));
                    insertString = "";
                    MysqlinsertString = "";
                }


                richTextBox1_S("\n total:" + total);

                var filename = pt.Name.Replace(":", textBox7.Text.ToString()).Replace(".", textBox8.Text.ToString()).Replace("/", textBox9.Text.ToString()).Replace("-", textBox10.Text.ToString());

                string csvPath = AppDomain.CurrentDomain.BaseDirectory + "CVS_SINGLE\\" + filename + ".csv";



                File.WriteAllText(csvPath, csvContent.ToString());
            }




            this.Invoke((MethodInvoker)delegate
            {
                richTextBox1_S("\n Export Done!");
                richTextBox1_S("\n\n #endregion!\n");
            });
        }

        private PointList SearchPIPoints(Server server, String condition)
        {
            return server.GetPoints(condition);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string output = string.Empty;
            var i = 0;
            PointList ptlist = server.GetPoints(textBox2.Text.ToString());
            output = "\n---------------------------------------------------------------show res of where , Count:" + ptlist.Count.ToString() + " ---------------------------------------------------------------";
            foreach (PIPoint p in ptlist)
            {
                i++;
                output += "\n" + i + "." + p.Name + " | PointType:" + p.PointType.ToString();

            }

            richTextBox1_S(output);

        }

        private void richTextBox1_S(string content)
        {

          
            fastColoredTextBox1.AppendText(content);

            // 设置选择开始的位置为文本长度，这样新的内容就会被选中
            //richTextBox1.SelectionStart = richTextBox1.Text.Length;

            // 通过ScrollToCaret方法滚动到选中内容的位置
            // richTextBox1.ScrollToCaret();
        }

        private void richTextBox3_S(string content)
        {

            fastColoredTextBox4.AppendText(content);

        }

        private void richTextBox4_S(string content)
        {

            fastColoredTextBox5.AppendText(content);

        }

        private void richTextBox5_S(string content)
        {

            fastColoredTextBox2.AppendText(content);

        }


        private void button5_Click(object sender, EventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork3;
            worker.RunWorkerAsync();
        }

        #region 作废
        //private void forall_byHour(PIPoint p, int i, int day)
        //{
        //    StringBuilder csvContent = new StringBuilder();
        //    int num = 0;
        //    this.Invoke((MethodInvoker)delegate
        //    {
        //        richTextBox1_S("\n" + (i + 1) + ".Underway [" + p.Name + "] Datas...");
        //    });

        //    DateTime startDay = DateTime.Now.AddDays(-day);
        //    DateTime endDay = DateTime.Now;


        //    var insertString = "";
        //    var pv = "";
        //    var count = 0;



        //    // 对每一个小时进行处理
        //    for (DateTime currentHour = startDay; currentHour < endDay; currentHour = currentHour.AddHours(1))
        //    {
        //        DateTime nextHour = currentHour.AddHours(1);
        //        var pitime = "";
        //        var pioriTime = "";
        //        int repeatCount = 0;
        //        // 获取单小时的数据
        //        PIValues piValues = p.Data.RecordedValues(currentHour.ToString(), nextHour.ToString(), BoundaryTypeConstants.btInside);




        //        foreach (PIValue piValue in piValues)
        //        {

        //            PITimeServer.PITime piTime = piValue.TimeStamp;

        //            pv = piValue.Value.ToString();
        //            if (piValue.Value.GetType().IsCOMObject)
        //                pv = ((DigitalState)piValue.Value).Name.ToString();



        //            pitime = piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss.fff");


        //            //考虑相同时间戳插值
        //            if (pioriTime == pitime)
        //            {
        //                repeatCount++;

        //                pitime = piTime.LocalDate.AddMilliseconds(repeatCount).ToString("yyyy-MM-dd HH:mm:ss.fff");

        //            }
        //            else
        //            {
        //                repeatCount = 0;
        //                pioriTime = pitime;
        //            }

        //            csvContent.AppendLine("'" + pitime + "'," + pv);

        //            insertString += "('" + pitime + "', '" + pv + "'),";


        //            //csvContent.AppendLine("'" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'," + pv);
        //            //insertString += "('" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss.fff") + "', '" + pv + "'),";
        //            num++;

        //            count++;
        //            if (count == 1000)
        //            {
        //                richTextBox1_S("\n count:" + count);

        //                sendPOSTBatch(p.Name, insertString.Substring(0, insertString.Length - 1));
        //                insertString = "";
        //                count = 0;

        //            }



        //            if (stopFlag == true)
        //            {
        //                break;
        //            }

        //        }

        //        if (count > 0)
        //        {
        //            richTextBox1_S("\n count:" + count);
        //            sendPOSTBatch(p.Name, insertString.Substring(0, insertString.Length - 1));
        //        }

        //    }







        //    if (num > 0)
        //    {
        //        string csvPath = AppDomain.CurrentDomain.BaseDirectory + "CVS_ALL_SINGLE\\" + p.Name.Replace(":", "__") + ".csv";
        //        File.WriteAllText(csvPath, csvContent.ToString());
        //    }

        //    this.Invoke((MethodInvoker)delegate
        //    {
        //        richTextBox1_S("\n Finished , total " + num + " rows");
        //    });
        //}





        //private void forall_byHour_old(PIPoint p, int i, int day)
        //{
        //    StringBuilder csvContent = new StringBuilder();
        //    int num = 0;
        //    this.Invoke((MethodInvoker)delegate
        //    {
        //        richTextBox1_S("\n" + (i + 1) + ".Underway [" + p.Name + "] Datas...");
        //    });

        //    DateTime startDay = DateTime.Now.AddDays(-day);
        //    DateTime endDay = DateTime.Now;




        //    // 对每一个小时进行处理
        //    for (DateTime currentHour = startDay; currentHour < endDay; currentHour = currentHour.AddHours(1))
        //    {
        //        DateTime nextHour = currentHour.AddHours(1);
        //        // 获取单小时的数据
        //        PIValues piValues = p.Data.RecordedValues(currentHour.ToString(), nextHour.ToString(), BoundaryTypeConstants.btInside);


        //        foreach (PIValue piValue in piValues)
        //        {

        //            PITimeServer.PITime piTime = piValue.TimeStamp;

        //            if (piValue.Value.GetType().IsCOMObject)
        //                csvContent.AppendLine("'" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss") + "'," + (DigitalState)piValue.Value);
        //            else
        //                csvContent.AppendLine("'" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss") + "'," + piValue.Value);
        //            num++;
        //        }

        //    }

        //    if (num > 0)
        //    {
        //        string csvPath = AppDomain.CurrentDomain.BaseDirectory + "CVS_ALL_SINGLE\\" + p.Name.Replace(":", "__") + ".csv";
        //        File.WriteAllText(csvPath, csvContent.ToString());
        //    }

        //    this.Invoke((MethodInvoker)delegate
        //    {
        //        richTextBox1_S("Finished , total " + num + " rows");
        //    });
        //}
        #endregion



        private void forall_byDay(PIPoint p, int i, int day)
        {
            StringBuilder csvContent = new StringBuilder();
            int num = 0;
            this.Invoke((MethodInvoker)delegate
            {
                richTextBox1_S("\n" + i + ".Underway [" + p.Name + "] Datas...");
            });

            DateTime startDay = DateTime.Now.AddDays(-day);
            DateTime endDay = DateTime.Now;

            DateTime ct = p.Data.Snapshot.TimeStamp.LocalDate;
            if (checkBox5.Checked)
            {
                 startDay = ct.AddDays(-day);
                 endDay = ct;
            }


            var insertString = "";
            var pv = "";
            var count = 0;
            var MysqlinsertString = "";



            // 对每一天进行处理
            for (DateTime currentDay = startDay; currentDay < endDay; currentDay = currentDay.AddDays(1))
            {
                DateTime nextDay = currentDay.AddDays(1);
                var pitime = "";
                var pioriTime = "";
                int repeatCount = 0;

                // 获取单日的数据
                PIValues piValues = p.Data.RecordedValues(currentDay.ToString(), nextDay.ToString(), BoundaryTypeConstants.btInside);

                foreach (PIValue piValue in piValues)
                {
                    PITimeServer.PITime piTime = piValue.TimeStamp;
                    pv = piValue.Value.ToString();
                    if (piValue.Value.GetType().IsCOMObject)
                        pv = ((DigitalState)piValue.Value).Name.ToString();
                    pitime = piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    //考虑相同时间戳插值
                    if (pioriTime == pitime)
                    {
                        repeatCount++;

                        pitime = piTime.LocalDate.AddMilliseconds(repeatCount).ToString("yyyy-MM-dd HH:mm:ss.fff");

                    }
                    else
                    {
                        repeatCount = 0;
                        pioriTime = pitime;
                    }

                    csvContent.AppendLine("'" + pitime + "'," + pv);

                    insertString += "('" + pitime + "', '" + pv + "'),";
                    MysqlinsertString += "('"+p.Name + "', '" + pitime + "', '" + pv + "'),";

                    num++;
                    count++;
                    if (count == 10000)
                    {
                        richTextBox1_S("\n count:" + count);

                        sendPOSTBatch(p.Name, insertString.Substring(0, insertString.Length - 1), MysqlinsertString.Substring(0, MysqlinsertString.Length - 1));
                        insertString = "";
                        MysqlinsertString = "";
                        count = 0;

                    }
                }
                if (count > 0)
                {
                    richTextBox1_S("\n count:" + count);
                    sendPOSTBatch(p.Name, insertString.Substring(0, insertString.Length - 1), MysqlinsertString.Substring(0, MysqlinsertString.Length - 1));
                    insertString = "";
                    MysqlinsertString = "";
                    count = 0;
                }
            }

            if (num > 0)
            {
                var filename = p.Name.Replace(":", textBox7.Text.ToString()).Replace(".", textBox8.Text.ToString()).Replace("/", textBox9.Text.ToString()).Replace("-", textBox10.Text.ToString());

                string csvPath = AppDomain.CurrentDomain.BaseDirectory + "CVS_ALL_SINGLE\\" + filename + ".csv";
                File.WriteAllText(csvPath, csvContent.ToString());

            }
            totalrow = totalrow+ num;




        }

        private void Worker_DoWork3(object sender, DoWorkEventArgs e)
        {
            string output = string.Empty;
            PIPoints piPoints = server.PIPoints;
            var day = Convert.ToInt16(textBox3.Text.ToString());

            //NamedValues nvsAttrs = new NamedValues();

            PointList ptlist = server.GetPoints(textBox2.Text.ToString());

            var i = 0;
           
            output = "\n #region "+DateTime.Now.ToUniversalTime() + " Export each tag to separate CSV files \n";
            richTextBox1_S(output);
            try
            {

                if (checkBox1.Checked)
                {

                    if (checkBox2.Checked)
                    {
                        if(string.IsNullOrEmpty(fastColoredTextBox3.Text))
                        {
                            MessageBox.Show("Can not connect Underway.\r\nDetail is: Tags Text Box is Empty!");
                            return;
                        }
                        else
                        {
                            var wc = "tag = '" + fastColoredTextBox3.Text.Replace(";", "' or tag = '") + "'";
                          
                            ptlist = server.GetPoints(wc.Replace("or tag = ''", ""));
                            //int count = 0;
                            //var pointNames = new HashSet<string>(fastColoredTextBox3.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                           
                            foreach (PIPoint p in ptlist)
                            {
                                totalrow = 0;
                                i++;
                                if (p.PointType.ToString().IndexOf("Digital") > -1)
                                {
                                    richTextBox1_S("\n" + i + ".Pname:" + p.Name + " |  PointType:" + p.PointType.ToString() + " not Underway XXXXXXXXXX");
                                   
                                    if (fastColoredTextBox2.Text.IndexOf(p.Name+";") > -1)
                                    {

                                    }
                                    else
                                    {
                                        richTextBox5_S(p.Name + ";\n");
                                    }
                                }
                                else
                                {
                                    forall_byDay(p, i, day);

                                }
                                richTextBox1_S("   Finished , total " + totalrow + " rows");
                            }
                        }

                      
                    }else
                    {
                        //#region 按天颗粒度
                        foreach (PIPoint p in ptlist)
                        {
                            totalrow = 0;
                            i++;
                            if (p.PointType.ToString().IndexOf("Digital") > -1)
                            {
                                richTextBox1_S("\n" + i + ".Pname:" + p.Name + " |  PointType:" + p.PointType.ToString() + " not Underway XXXXXXXXXX");
                               
                                if (fastColoredTextBox2.Text.IndexOf(p.Name+";") > -1)
                                {

                                }
                                else
                                {
                                    richTextBox5_S(p.Name + ";\n");

                                }
                            }
                            else
                            {
                                forall_byDay(p, i, day);

                            }
                            richTextBox1_S("   Finished , total " + totalrow + " rows");
                            
                        }
                        //#endregion
                    }





                    #region 按小时颗粒度
                    //foreach (PIPoint p in ptlist)
                    //{
                    //    forall_byHour(p, i, day);
                    //    i++;
                    //}
                    #endregion
                }
                //else
                //{
                //    foreach (PIPoint p in piPoints)
                //    {
                //        StringBuilder csvContent = new StringBuilder();
                //        //csvContent.AppendLine("tag,time,value");
                //        int num = 0;
                //        this.Invoke((MethodInvoker)delegate
                //        {
                //            richTextBox1_S("\n" + (i + 1) + ".Underway [" + p.Name + "] Datas...");
                //        });

                //        PIValues piValues = p.Data.RecordedValues(DateTime.Now.AddDays(-day).ToString(), DateTime.Now.ToString());

                //        foreach (PIValue piValue in piValues)
                //        {
                //            PITimeServer.PITime piTime = piValue.TimeStamp;

                //            if (piValue.Value.GetType().IsCOMObject)
                //                //状态值
                //                csvContent.AppendLine("'" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss") + "'," + (DigitalState)piValue.Value);
                //            else
                //                //模拟值
                //                csvContent.AppendLine("'" + piTime.LocalDate.ToString("yyyy-MM-dd HH:mm:ss") + "'," + piValue.Value);
                //            num++;

                //        }
                //        i++;

                //        this.Invoke((MethodInvoker)delegate
                //        {
                //            richTextBox1_S("Finished ,total " + num + " rows");
                //        });

                //        string csvPath = AppDomain.CurrentDomain.BaseDirectory + "CVS_ALL_SINGLE\\" + p.Name.Replace(":", "__") + ".csv";

                //        File.WriteAllText(csvPath, csvContent.ToString());
                //    }

                //}

                this.Invoke((MethodInvoker)delegate
                {
                    richTextBox1_S("\n Export Done!");

                    richTextBox1_S("\n\n #endregion!\n");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Can not connect to PI Server.\r\nDetail is: " + ex.Message);
                return;
            }


        }


        public string sendPOST(string apiurl, string date, string jsonstr)
        {
            // initial
            string result = "";

            try
            {
                richTextBox1_S("\n Up to TD Datas...");
                var client = new RestClient(apiurl);
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                //var body = jsonstr;
                var body = @"insert into " + textBox6.Text + " values('" + date + "','" + jsonstr + "')";
                richTextBox1_S("\n " + body);


                request.AddHeader("Authorization", "Basic cm9vdDp0YW9zZGF0YQ==");
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);

                result = response.Content;
                richTextBox1_S("\n " + result);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return null;

        }

        public string sendPOSTBatch(string table, string data, string mysqldata)
        {
            // initial
            string result = "";
            var apiurl = "http://" + textBox5.Text + ":6041/rest/sql";

            var Failtag = table;

            table = table.Replace(":", textBox7.Text.ToString()).Replace(".", textBox8.Text.ToString()).Replace("/", textBox9.Text.ToString()).Replace("-", textBox10.Text.ToString());
            try
            {
              

                if(checkBox4.Checked)
                {
                    richTextBox1_S(" Up to TD Datas...");
                    var client1 = new RestClient(apiurl);
                    client1.Timeout = -1;
                    var request1 = new RestRequest(Method.POST);


                    var body1 = @"CREATE TABLE IF NOT EXISTS " + textBox6.Text + "." + table + @" (ts TIMESTAMP,val NCHAR(30)); ";
                    request1.AddHeader("Authorization", "Basic cm9vdDp0YW9zZGF0YQ==");
                    request1.AddHeader("Content-Type", "application/json");
                    request1.AddParameter("application/json", body1, ParameterType.RequestBody);
                    IRestResponse response1 = client1.Execute(request1);
                    var result1 = response1.Content;

                    if(result1.IndexOf("{\"code\":0,")>-1)
                    {
                        var client2 = new RestClient(apiurl);
                        client2.Timeout = -1;
                        var request2 = new RestRequest(Method.POST);


                        var body2 = @"insert into " + textBox6.Text + "." + table + " values" + data + "; ";
                        request2.AddHeader("Authorization", "Basic cm9vdDp0YW9zZGF0YQ==");
                        request2.AddHeader("Content-Type", "application/json");
                        request2.AddParameter("application/json", body2, ParameterType.RequestBody);
                        IRestResponse response2 = client2.Execute(request2);
                        var result2 = response2.Content;

                        if (result2.IndexOf("{\"code\":0,") > -1)
                        {
                          
                            int startIndex = result2.IndexOf("data\":[[") + 8;
                            int endIndex = result2.IndexOf("]],\"row");
                            richTextBox1_S(" Suceess " + result2.Substring(startIndex, endIndex - startIndex) + " rows.");
                            richTextBox1_S(" Post Done");
                        }
                        else
                        {
                            richTextBox1_S(" Fail !");
                            if (fastColoredTextBox4.Text.IndexOf(Failtag) >-1)
                            {
                               
                            }
                            else
                            {
                                richTextBox3_S(Failtag + ";\n");
                            }
                           
                        }

                           
                    }
                    else
                    {
                        richTextBox1_S(" Fail !");
                        if (fastColoredTextBox4.Text.IndexOf(Failtag) > -1)
                        {
                            
                        }
                        else
                        {
                            richTextBox3_S(Failtag + ";\n");
                        }

                    }





                }


                if (checkBox3.Checked)
                {
                   


                    // 使用using确保正确释放连接资源
                    using (MySqlConnection conn = new MySqlConnection(textBox11.Text))
                    {
                        richTextBox1_S(" Up to Mysql Datas...");
                        try
                        {
                            // 打开连接
                            conn.Open();
                          
                            //// 创建插入命令
                            string insertQuery = "INSERT INTO pi_data (tagname, ts,value) VALUES "+ mysqldata + ";";

                            // 创建命令对象
                            using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                            {
                               
                                // 执行命令
                                int Mysqlresult = cmd.ExecuteNonQuery();

                                richTextBox1_S(" Suceess " + Mysqlresult + " rows.");
                                richTextBox1_S(" Post Done");

                              
                            }
                        }
                        catch (Exception ex)
                        {
                            richTextBox1_S( ex.Message);


                            if (fastColoredTextBox5.Text.IndexOf(Failtag) > -1)
                            {
                                // 如果找到 Failtag，执行这里的代码
                            }
                            else
                            {
                                richTextBox4_S(Failtag + ";\n");
                            }

                        }
                    }


                  

            
                   
                }






                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return null;

        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (stopFlag == false)
                stopFlag = true;
            else
                stopFlag = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string connectionString = textBox11.Text;

            // 使用using确保正确释放连接资源
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    // 打开连接
                    conn.Open();
                    richTextBox1_S("1");
                    //// 创建插入命令
                    //string insertQuery = "INSERT INTO table_name (column1, column2) VALUES (@value1, @value2)";

                    //// 创建命令对象
                    //using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    //{
                    //    // 使用参数化查询防止SQL注入
                    //    cmd.Parameters.AddWithValue("@value1", "Value1");
                    //    cmd.Parameters.AddWithValue("@value2", "Value2");

                    //    // 执行命令
                    //    int result = cmd.ExecuteNonQuery();

                    //    // 输出受影响的行数
                    //    Console.WriteLine("Rows affected: " + result);
                    //}
                }
                catch (Exception ex)
                {
                    richTextBox1_S("Exception"+ ex.Message);
                    
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {



            // if (string.IsNullOrEmpty(textBox1.Text.ToString()))
            // {
            //     MessageBox.Show("tag textbox is Empty , please enter tag");
            //     return;
            // }
            // string output = string.Empty;
            // pt = server.PIPoints[textBox1.Text.ToString()]; //SINUSOID
            // StringBuilder csvContent = new StringBuilder();
            // var day = Convert.ToInt16(textBox3.Text.ToString());

            //// output = "\n--------------------------------------------------- Export single tag to CVS files ---------------------------------------------------";
            // output = "\n #region " + DateTime.Now.ToUniversalTime() + " Export single tag to CVS files \n";
            // richTextBox1_S(output);




            // DateTime startTime = DateTime.Now.AddDays(-day);
            // DateTime endTime = DateTime.Now;

            // var currentValue = pt.Data.Snapshot;
            // richTextBox1_S("\n currentValue:" + pt.Data.Snapshot.TimeStamp.LocalDate.ToString() + "");



            // PIValues values = pt.Data.RecordedValues(
            // StartTime: startTime.ToString(),
            // EndTime: endTime.ToString(),
            // BoundaryType: PISDK.BoundaryTypeConstants.btInside,
            // filterExp: "",
            // ShowFiltered: PISDK.FilteredViewConstants.fvShowFilteredState);




            //int countToFetch = Math.Min(10000, values.Count);
            //richTextBox1_S("\n countToFetch:" + countToFetch + "");
            //int batchSize = 10000; // 设置每批次读取的记录数
            //string batchEndTime = DateTime.Parse(startTime.ToString()).AddHours(batchSize).ToString();

            //int recordCount = pt.Data.RecordedValuesAvailable(
            //       StartTime: startTime.ToString(),
            //       EndTime: endTime.ToString(),
            //       BoundaryType: BoundaryTypeConstants.btInside);

            //richTextBox1_S("\n batchEndTime:" + batchEndTime.ToString() + "");





        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox6_Enter(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {
            fastColoredTextBox3.Clear();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            fastColoredTextBox1.Clear();
        }

        private void button7_Click_1(object sender, EventArgs e)
        {
            fastColoredTextBox4.Clear();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            fastColoredTextBox5.Clear();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            fastColoredTextBox2.Clear();
        }

        private void fastColoredTextBox3_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            // 暂时解除 TextChanged 事件的绑定
            fastColoredTextBox3.TextChanged -= fastColoredTextBox3_TextChanged;

            fastColoredTextBox3.BeginUpdate();

            // 替换分号为分号加换行符，然后去除连续的换行符（或空行）
            string newText = fastColoredTextBox3.Text.Replace(";", ";\n");
            newText = Regex.Replace(newText, @"^\s*$\n|\r", "", RegexOptions.Multiline).Trim();

            // 设置新文本
            fastColoredTextBox3.Text = newText;

            fastColoredTextBox3.EndUpdate();

           

            // 重新绑定 TextChanged 事件
            fastColoredTextBox3.TextChanged += fastColoredTextBox3_TextChanged;
            // 更新行数显示
            label16.Text = fastColoredTextBox3.LinesCount.ToString();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            fastColoredTextBox1.CollapseAllFoldingBlocks();

            // 刷新控件以应用折叠
            fastColoredTextBox1.Refresh();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            fastColoredTextBox1.ExpandAllFoldingBlocks();

            // 刷新控件以应用折叠
            fastColoredTextBox1.Refresh();
        }

        private void fastColoredTextBox1_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            label12.Text = (fastColoredTextBox1.LinesCount - 1).ToString();
        }

        private void fastColoredTextBox2_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            label13.Text = (fastColoredTextBox2.LinesCount - 1).ToString();
        }

        private void fastColoredTextBox4_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            label14.Text = (fastColoredTextBox4.LinesCount-1).ToString();
        }

        private void fastColoredTextBox5_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            label15.Text = (fastColoredTextBox5.LinesCount-1).ToString();
        }

        private void r(object sender, EventArgs e)
        {

        }
    }
}
