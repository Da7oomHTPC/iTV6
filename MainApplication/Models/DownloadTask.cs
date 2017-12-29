﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace iTV6.Models
{

    enum DownloadState
    {
        Pending,
        Downloading,
        Completed,
        Falied
    }
    class DownloadTask
    {
        public string Channel;
        public string Source;
        public DateTime StartTime;
        public DateTime EndTime;
        public Uri RequestUri;
        public string FileName;
        public StorageFolder Folder;

        public int Progress = 0;
        public DownloadState State=DownloadState.Pending;

        private bool stopped = true;
        public DownloadTask(string channel, string source, Uri requestUri, StorageFolder storageFolder, DateTime startTime, DateTime endTime)
        {
            Channel = channel;
            Source = source;
            RequestUri = requestUri;
            Folder = storageFolder;
            StartTime = startTime;
            EndTime = endTime;
            stopped = false;
            Download();
        }

        private async void Download()
        {
            //当前时间
            DateTime currentTime = System.DateTime.Now;
            //起始时间与当前时间的间隔
            TimeSpan intervalTime = StartTime - currentTime;
            bool startTimeOK = (intervalTime.TotalMilliseconds < 5000);
            //等到系统时间到达startTime，才开始录制工作
            while (!startTimeOK)
            {
                await Task.Delay(5000);
                currentTime = System.DateTime.Now;
                intervalTime = StartTime - currentTime;
                startTimeOK = (intervalTime.TotalMilliseconds < 5000);   //认为差距时间在5s内，则已到达预定时间，开始录制
            }
            //录制时间的长度
            TimeSpan durationTime = EndTime - StartTime;
            //需要改进
            int m3u8Time;
            //源是清华的话，每个m3u8文件记录的长度为1分钟，单位：min
            if (Source == "清华")
            {
                m3u8Time = 1;
            }
            else//其他源需要协商
            {
                m3u8Time = 1;
            }
            int recordNum = (int)(durationTime.TotalMinutes) / m3u8Time;
            //创建目录，创建文件流
            StorageFile sampleFile = null;
            String storageName = Channel + "_" + Source + "_" +
                              StartTime.Year.ToString() + "_" + StartTime.Month.ToString() + "_" + StartTime.Day.ToString() + "_" + StartTime.Hour.ToString() + "_" + StartTime.Minute.ToString();  //文件名
            sampleFile = await Folder.CreateFileAsync(storageName + ".ts", CreationCollisionOption.OpenIfExists);
            var stream = await sampleFile.OpenAsync(FileAccessMode.ReadWrite);//得到文件流
            //每m3u8Time个时间，获取一次url，得到的ts文件写入文件流中
            for (int j = 0; j < recordNum; j++)
            {
                //StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                // await storageFolder.CreateFileAsync("sample.txt", CreationCollisionOption.ReplaceExisting);
                //获取m3u8文件
                Windows.Web.Http.HttpClient http = new Windows.Web.Http.HttpClient();
                var buffer = await http.GetBufferAsync(RequestUri);
                //处理m3u8文件，缓存至text中
                DataReader TempData = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
                string text = TempData.ReadString(buffer.Length);
                //存储所有当前.ts文件的名字
                ArrayList tsContent = TSposition(text);
                string tempTs;
                string uri = RequestUri.ToString();
                int hlsPos = uri.IndexOf("hls");
                Uri tempTsUri;
                //sampleFile = await storageFolder.CreateFileAsync("sample.ts", CreationCollisionOption.ReplaceExisting);

                foreach (object i in tsContent)
                {
                    tempTs = i.ToString();
                    if (tempTs.StartsWith("http"))//m3u8中的可能就是完整地址
                        tempTsUri = new Uri(tempTs);
                    else//其他情况需要加上前面一段
                        tempTsUri = new Uri(uri.Substring(0, hlsPos + ("hls/").Length) + tempTs, UriKind.RelativeOrAbsolute);
                    http = new Windows.Web.Http.HttpClient();

                    buffer = await http.GetBufferAsync(tempTsUri);

                    using (var outputStream = stream.GetOutputStreamAt(stream.Size))//获得输出文件流，并定位到流的末端
                    {
                        using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                        {
                            dataWriter.WriteBuffer(buffer);//将buffer写入文件流
                            await dataWriter.StoreAsync();
                            await outputStream.FlushAsync();
                        }
                    }
                }

                await Task.Delay(m3u8Time * 60 * 1000);

            }
            stream.Dispose();//关闭文件流

        }

        public ArrayList TSposition(string text)
        {
            ArrayList newlist = new ArrayList();
            int i = 0;
            int tempPos = 0;
            while (i < text.Length)
            {
                int ts_length = 0;
                tempPos = text.IndexOf(".ts", i);
                if (tempPos == -1)
                    break;
                ts_length = tempPos - text.LastIndexOf("\n", tempPos) - 1 + 3;//从当前.ts的位置向前找“\n”，从而得到.ts文件名的长度
                newlist.Add(text.Substring(tempPos - ts_length + 3, ts_length));
                i = tempPos + 3;
            }
            return newlist;
        }
    }
}