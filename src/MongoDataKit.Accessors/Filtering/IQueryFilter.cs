using MongoDB.Driver;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Core.Paging;

namespace MongoDataKit.Accessors.Filtering;

public interface IQueryFilter<TDocument> : IPaginationOptions
    where TDocument : IEntity
{
    FilterDefinition<TDocument> ToFilterDefinition();
    SortDefinition<TDocument> ToSortDefinition();
}
