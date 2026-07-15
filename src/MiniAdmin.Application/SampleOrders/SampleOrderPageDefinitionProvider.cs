using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.SampleOrders;

public sealed class SampleOrderPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "business.sample-order",
            ParentKey: null,
            Path: "/business/sample-order",
            Component: "/business/sample-order/index",
            Redirect: null,
            Icon: "lucide:shopping-basket",
            Order: 110,
            I18nKey: "page.business.sampleOrder.title",
            Title: new LocalizedText("示例订单", "Sample Orders"),
            IsVisible: true,
            Permissions:
            [
                Permission("query", "查询示例订单", "Query sample orders", "e66cc1d7-d57d-336c-beda-385f10d8d22c"),
                Permission("create", "创建示例订单", "Create sample orders", "51e067a7-5a49-e77e-89be-d49658ce87c7"),
                Permission("update", "更新示例订单", "Update sample orders", "db3fdd2b-be96-e533-7a35-85522b1a6cbb"),
                Permission("delete", "删除示例订单", "Delete sample orders", "26601485-c5c3-1fc6-6606-0fa51f39b5b1"),
                Permission("submit-workflow", "提交示例订单审批", "Submit sample order workflow", "b6bc69a7-64f2-4ec0-8f7c-7c57c3168e53"),
                Permission("withdraw-workflow", "撤回示例订单审批", "Withdraw sample order workflow", "0ef55a19-3b0e-49ad-8e38-68f6723b3689")
            ],
            Id: Guid.Parse("ea6ba4c5-2c6a-aaff-2061-c477e14d4c4f"));
    }

    private static PermissionDefinition Permission(
        string action,
        string zhCn,
        string enUs,
        string id)
    {
        return new PermissionDefinition(
            $"business:sample-order:{action}",
            "business.sample-order",
            action,
            $"permission.business.sampleOrder.{action}",
            new LocalizedText(zhCn, enUs),
            Guid.Parse(id));
    }
}
