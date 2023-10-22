using LiteDB.Async;
using NLog;
using BsonDocument = LiteDB.BsonDocument;
using BsonValue = LiteDB.BsonValue;

namespace Core.Storage.DB
{
    public class Table<T>
    {
        protected static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
        protected EmbeddedDB db;
        //���ﲻֱ��ʹ��T���ͣ�litedb�ײ����async task��Ȼ���ڶ��߳����⣬�����첽�ӿڵײ㵥�̣߳�Ϊ�˼����߳�ѹ�����������л��������л��ŵ��ⲿ��
        protected ILiteCollectionAsync<BsonDocument> collection;
        readonly string realName;
        private BsonDocument InnerSerialize(T value) => db.InnerDB.UnderlyingDatabase.Mapper.ToDocument(value);
        private T InnerDeserialize(BsonDocument value) => db.InnerDB.UnderlyingDatabase.Mapper.Deserialize<T>(value);

        internal Table(EmbeddedDB db, string name, string realName)
        {
            this.db = db;
            this.realName = realName;
            collection = db.InnerDB.GetCollection<BsonDocument>(name);
        }

        public Task<bool> Set<IDType>(IDType id, T value)
        {
            return collection.UpsertAsync(new BsonValue(id), InnerSerialize(value));
        }


        public Task<int> SetBatch(List<T> values)
        {
            var list = new List<BsonDocument>(values.Count);
            values.ForEach(x => list.Add(InnerSerialize(x)));
            return collection.UpsertAsync(list);
        }

        public Task<bool> Delete<IDType>(IDType id)
        {
            return collection.DeleteAsync(new BsonValue(id));
        }

        public async Task<T> Get<IDType>(IDType id)
        {
            try
            {
                var ret = await collection.FindByIdAsync(new BsonValue(id));
                return InnerDeserialize(ret);
            }
            catch (Exception e)
            {
                LOGGER.Error($"Get {realName} [{id}] error:{e}");
            }
            return default;
        }

        public async Task<bool> Exist<IDType>(IDType id)
        {
            var ret = await collection.FindByIdAsync(new BsonValue(id));
            return ret != null;
        }

        public async Task<List<T>> GetAll()
        {
            var result = new List<T>();
            var ret = await collection.FindAllAsync();
            foreach (var item in ret)
            {
                result.Add(InnerDeserialize(item));
            }
            return result;
        }

        public async Task<bool> Any()
        {
            return await collection.CountAsync() > 0;
        }
    }
}
