using MediatR;

namespace SportsApp.Requests
{
    public interface IHttpRequest : IRequest<IResult>
    {
    }
}
