using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.Customers;

public sealed class CustomerPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "business.customer",
            ParentKey: null,
            Path: "/business/customer",
            Component: "/business/customer/index",
            Redirect: null,
            Icon: "lucide:contact-round",
            Order: 100,
            I18nKey: "page.business.customer.title",
            Title: new LocalizedText("客户资料", "Customers"),
            IsVisible: true,
            Permissions:
            [
                Permission("query", "查询客户资料", "Query customers", "daaa964d-801a-0f36-690f-5226d7307dac"),
                Permission("create", "创建客户资料", "Create customers", "984fb2f8-98e6-328a-c63e-d2b0a19cced5"),
                Permission("update", "更新客户资料", "Update customers", "c4ee2db6-3d3c-432c-77c4-e0d03839534d"),
                Permission("delete", "删除客户资料", "Delete customers", "401f189a-7c24-b410-b061-81b74ace666b")
            ],
            Id: Guid.Parse("13dd0b09-ae84-eaf2-8de8-63b98e6ae903"));
    }

    private static PermissionDefinition Permission(
        string action,
        string zhCn,
        string enUs,
        string id)
    {
        return new PermissionDefinition(
            $"business:customer:{action}",
            "business.customer",
            action,
            $"permission.business.customer.{action}",
            new LocalizedText(zhCn, enUs),
            Guid.Parse(id));
    }
}
