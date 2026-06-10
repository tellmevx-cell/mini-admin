using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Notices;

namespace MiniAdmin.Application.Notices;

public sealed class NoticeAppService(INoticeRepository noticeRepository) : INoticeAppService
{
    public Task<PageResult<NoticeDto>> GetListAsync(
        NoticeListQuery query,
        CancellationToken cancellationToken = default)
    {
        return noticeRepository.GetListAsync(query, cancellationToken);
    }

    public Task<NoticeDto> CreateAsync(
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        return noticeRepository.CreateAsync(request, cancellationToken);
    }

    public Task<NoticeDto?> UpdateAsync(
        Guid id,
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        return noticeRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return noticeRepository.DeleteAsync(id, cancellationToken);
    }
}
