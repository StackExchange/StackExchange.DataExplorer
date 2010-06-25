using System;
using System.Collections.Generic;
using System.Linq;

namespace StackExchange.DataExplorer.Helpers
{
    public interface IPagedList
    {
        int TotalCount { get; set; }
        int PageCount { get; set; }
        int PageIndex { get; set; }
        int PageSize { get; set; }
    }

    public class PagedList<T> : List<T>, IPagedList
    {
        public PagedList(IQueryable<T> source, int index, int pageSize)
        {
            TotalCount = source.Count();
            PageSize = pageSize;
            SetPageIndex(index);
            SetPageCount();

            AddRange(source.Skip(PageIndex*PageSize).Take(PageSize).ToList());
        }

        public PagedList(IEnumerable<T> source, int index, int pageSize)
        {
            TotalCount = source.Count();
            PageSize = pageSize;
            SetPageIndex(index);
            SetPageCount();

            AddRange(source.Skip(PageIndex*PageSize).Take(PageSize).ToList());
        }


        /// <summary>
        /// For API use - takes an already paged list with a known total count; doesn't do further paging operations.
        /// </summary>
        /// <remarks>
        /// Allows this new PagedList to store the paging properties of a previous PagedList without calling Convert.
        /// We need this when paging on the cached lists of post ids and converting them to Api Questions.
        /// This is such an awful hack.
        /// </remarks>
        public PagedList(List<T> pagedSource, IPagedList other)
        {
            TotalCount = other.TotalCount;
            PageSize = other.PageSize;
            PageCount = other.PageCount;
            PageIndex = other.PageIndex;
            AddRange(pagedSource);
        }

        private PagedList()
        {
        }

        public bool IsPreviousPage
        {
            get { return (PageIndex > 0); }
        }

        public bool IsNextPage
        {
            get { return (PageIndex*PageSize) <= TotalCount; }
        }

        #region IPagedList Members

        public int TotalCount { get; set; }
        public int PageCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }

        #endregion

        public IEnumerable<IEnumerable<T>> ToRows(int totalRows)
        {
            return this.Select((item, index) => new {Item = item, Index = index})
                .GroupBy(o => o.Index%totalRows)
                .Select(g => g.Select(o => o.Item));
        }

        private void SetPageIndex(int index)
        {
            // our urls assume 1-based indexing into pages; convert to 0-based and don't allow negatives
            PageIndex = index < 1 ? 0 : index - 1;
        }

        /// <summary>
        /// Should be called *after* TotalCount and PageSize have been initialized.
        /// </summary>
        private void SetPageCount()
        {
            int remainder = TotalCount%PageSize;
            PageCount = (TotalCount/PageSize) + (remainder == 0 ? 0 : 1); // only need another page if we have spillover
        }


        /// <summary>
        /// Answers a new PagedList of <typeparamref name="TNew"/> that is created by calling <paramref name="converter"/> on
        /// each element within this list's paging window.
        /// </summary>
        /// <typeparam name="TNew">The type that the resulting PagedList will be of.</typeparam>
        /// <param name="converter">Function that will return the replacement for each item in this PagedList, usually a hydration via DBContext.</param>
        public PagedList<TNew> Convert<TNew>(Func<T, TNew> converter)
        {
            // retain all the paging properties
            var result = new PagedList<TNew>
                             {
                                 TotalCount = TotalCount,
                                 PageCount = PageCount,
                                 PageIndex = PageIndex,
                                 PageSize = PageSize
                             };

            // call the converter to generate the data we want in the resulting list
            foreach (T item in this)
            {
                result.Add(converter(item));
            }

            return result;
        }
    }

    public static class PagedListExtensions
    {
        public static IEnumerable<IEnumerable<T>> Transpose<T>(this IEnumerable<IEnumerable<T>> source)
        {
            return from row in source
                   from col in row.Select(
                       (x, i) => new KeyValuePair<int, T>(i, x))
                   group col.Value by col.Key
                   into c
                   select c as IEnumerable<T>;
        }

        public static PagedList<T> ToPagedList<T>(this IEnumerable<T> source, int index)
        {
            return ToPagedList(source, index, null);
        }

        public static PagedList<T> ToPagedList<T>(this IEnumerable<T> source, int index, int? pageSize)
        {
            if (!pageSize.HasValue || pageSize.Value < 1)
                pageSize = PageSizer.DefaultPageSize;

            return new PagedList<T>(source, index, pageSize.Value);
        }

        public static PagedList<T> ToPagedList<T>(this IQueryable<T> source, int index, int? pageSize)
        {
            if (!pageSize.HasValue || pageSize.Value < 1)
                pageSize = PageSizer.DefaultPageSize;

            return new PagedList<T>(source, index, pageSize.Value);
        }

        public static PagedList<T> ToPagedList<T>(this IQueryable<T> source, int index)
        {
            return ToPagedList(source, index, null);
        }
    }
}