﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MsgPack;
using MsgPack.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;
using WBuffer = Windows.Storage.Streams.Buffer;
using iTV6.Services;

namespace iTV6.Models
{
    /// <summary>
    /// 频道的Model。频道为近似单例模式
    /// </summary>
    public class Channel
    {
        #region 频道的获取
        private Channel() { }
        private static Dictionary<string, Channel> _instances = new Dictionary<string, Channel>();
        /// <summary>
        /// 根据名称和分类获得频道实例
        /// </summary>
        /// <param name="name">频道名称</param>
        /// <param name="type">频道分类</param>
        public static Channel GetChannel(string uniqueKey, string name, ChannelType type)
        {
            if (!_instances.ContainsKey(uniqueKey))
                _instances.Add(uniqueKey, new Channel() { Name = name, Type = type, UniqueId = uniqueKey });
            return _instances[uniqueKey];
        }
        /// <summary>
        /// 根据名称获得频道实例
        /// </summary>
        /// <param name="name">频道名称</param>
        public static Channel GetChannel(string uniqueKey, string name)
        {
            return GetChannel(uniqueKey, name, GetChannelTypeByName(name));
        }

        public static Channel GetChannel(string uniqueKey)
        {
            if(_instances.ContainsKey(uniqueKey))
                return _instances[uniqueKey];
            else
            {
                LoggingService.Debug("Model", "访问了未知的频道：" + uniqueKey);
                return null;
            }
        }

        /// <summary>
        /// 根据名称推测频道分类列表
        /// </summary>
        /// <param name="channelName">统一后的频道名称</param>
        /// <returns>频道类型</returns>
        public static ChannelType GetChannelTypeByName(string channelName)
        {
            ChannelType result = ChannelType.Standard;
            if (channelName.IndexOf("高清") > -1)
                result = ChannelType.Hd;
            if (channelName.IndexOf("CCTV") > -1 || channelName.IndexOf("CGTN") > -1)
                return result | ChannelType.Central;
            if (channelName.IndexOf("卫视") > -1)
                return result | ChannelType.Local;
            if (channelName.IndexOf("北京") > -1)
                return result | ChannelType.Local | ChannelType.Beijing;
            if (channelName.IndexOf("教育电视台") > -1 || channelName.IndexOf("CHC") > -1 ||
                channelName.IndexOf("卡通") > -1 || channelName.IndexOf("党建") > -1)
                return result | ChannelType.Local;
            return result | ChannelType.Special;
        }

        #endregion

        /// <summary>
        /// 频道名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 频道类型
        /// </summary>
        public ChannelType Type { get; set; }

        /// <summary>
        /// 用来获取LOGO的TV猫ID
        /// </summary>
        public string LogoID { get; set; }

        public override string ToString() => $"{Name}[{UniqueId}]"; // 调试用

        #region 缓存和序列化

        const string storageFile = "channels_cache.dat";
        private static readonly SerializationContext channelContext = new SerializationContext();
        static Channel()
        {
            channelContext.Serializers.RegisterOverride(new ChannelSerializer(channelContext));
            CacheService.RegisterCacheFile(storageFile);
        }

        /// <summary>
        /// 将频道列表缓存
        /// </summary>
        public static async Task StoreChannels()
        {
            var storage = await ApplicationData.Current.LocalFolder.CreateFileAsync(storageFile, CreationCollisionOption.ReplaceExisting);
            var serializer = MessagePackSerializer.Get<IEnumerable<Channel>>(channelContext);
            using (var stream = await storage.OpenStreamForWriteAsync())
                serializer.Pack(stream, _instances.Values);
        }

        /// <summary>
        /// 从缓存中恢复频道列表
        /// </summary>
        public static async Task RestoreChannels()
        {
            var storage = await ApplicationData.Current.LocalFolder.TryGetItemAsync(storageFile) as StorageFile;
            if (storage != null)
            {
                using (var stream = await storage.OpenStreamForReadAsync())
                    MessagePackSerializer.Get<IEnumerable<Channel>>(channelContext).Unpack(stream);
            }
        }

        public class ChannelSerializer : MessagePackSerializer<Channel>
        {
            public ChannelSerializer(SerializationContext ownerContext) : base(ownerContext) { }

            protected override void PackToCore(Packer packer, Channel obj)
            {
                packer.PackString(obj.Name);
                packer.PackString(obj.UniqueId);
                packer.Pack((byte)obj.Type);
            }

            protected override Channel UnpackFromCore(Unpacker unpacker)
            {
                string name = unpacker.Unpack<string>();
                unpacker.Read();
                string id = unpacker.Unpack<string>();
                unpacker.Read();
                ChannelType ctype = (ChannelType)Enum.ToObject(typeof(ChannelType), unpacker.Unpack<byte>());
                return GetChannel(id, name, ctype);
            }
        }

        /// <summary>
        /// 在Channel字典已经加载完成的情况下可以使用该序列化对象
        /// </summary>
        public class ExistingChannelSerializer : MessagePackSerializer<Channel>
        {
            public ExistingChannelSerializer(SerializationContext ownerContext) : base(ownerContext)
            {
#if DEBUG
                if (_instances.Count == 0) throw new InvalidDataException("使用Key序列化Channel之前应初始化Channel的实例字典");
#endif
            }

            protected override void PackToCore(Packer packer, Channel obj) =>
                packer.PackString(obj.UniqueId);

            protected override Channel UnpackFromCore(Unpacker unpacker) =>
                GetChannel(unpacker.Unpack<string>());
        }

        #endregion
    }

    /// <summary>
    /// 频道类型
    /// </summary>
    [Flags]
    public enum ChannelType
    {
        Central = 1,    //中央电视台
        Local = 2,      //地方电视台
        Special = 4,    //特色频道
        Radio = 8,      //广播
        Standard = 16,  //标清
        Hd = 32,        //高清
        Beijing = 64    //北京
    }
}
