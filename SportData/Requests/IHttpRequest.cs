using MediatR;

namespace SportData.Requests
{
    public interface IHttpRequest : IRequest<IResult>
    {
    }
}
