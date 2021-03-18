using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Paginating;
using Fluid.Values;

namespace Fluid.Tests.Paginating
{
    public class IdListPaginateValue : PaginationValue
    {
        private readonly List<int> _dataList;

        public IdListPaginateValue()
        {
            _dataList = Enumerable.Range(1, 100).ToList();
        }

        protected override string GetUrl(int page)
        {
            return $"index.aspx?page={page}";
        }

        protected override Task<QueryResult> QueryAsync(int page, int pageSize)
        {
            var list = _dataList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => NumberValue.Create(i));
            var result = new QueryResult
            {
                ItemCount = _dataList.Count
            };
            result.AddRange(list);
            return Task.FromResult(result);
        }
    }
}