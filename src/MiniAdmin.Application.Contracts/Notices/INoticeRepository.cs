using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Notices;

public interface INoticeRepository
{
    Task<PageResult<NoticeDto>> GetListAsync(
        NoticeListQuery query,
        CancellationToken cancellationToken = default);

    Task<NoticeDto> CreateAsync(
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default);

    Task<NoticeDto?> UpdateAsync(
        Guid id,
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
