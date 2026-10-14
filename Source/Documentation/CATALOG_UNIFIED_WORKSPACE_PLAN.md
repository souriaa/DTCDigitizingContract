# Kế hoạch gộp danh mục và quản lý sản phẩm/dịch vụ vào một trang

Ngày lập: 2026-10-14. Phạm vi: Frontend tenant và API đọc dữ liệu catalog cần thiết cho UI mới.

**Hiện trạng triển khai: đã bổ sung bước chuẩn bị bắt buộc phân nhóm khi tạo/sửa. Trang UI gộp và API đọc phục vụ cây của Phase 1–6 chưa triển khai.**

## 1. Mục tiêu và giới hạn

Gộp bốn màn hình danh mục sản phẩm, sản phẩm, loại dịch vụ và dịch vụ thành một khu vực duy nhất, có hai tab **Sản phẩm** và **Dịch vụ**. Người dùng nhìn thấy danh mục trước, bấm mở để xem sản phẩm/dịch vụ bên trong; thao tác quản lý danh mục và từng mặt hàng thực hiện ngay tại đây.

- Không gộp các bảng database; không đổi ID hoặc các cột quan hệ hiện tại.
- Không thay đổi luồng chọn sản phẩm/dịch vụ khi tạo/sửa hợp đồng, báo giá hoặc nội dung snapshot đã chốt.
- Giữ nghiệp vụ tạo/sửa, khóa/mở, xóa mềm, khôi phục và kiểm tra tham chiếu hiện có.
- Không thêm cột JSON, converter, fallback schema hoặc luồng duy trì dữ liệu cũ. JSON của request/response API không phải lưu dữ liệu trong cột JSON.
- Chỉ thêm API đọc/projection và tham số lọc khi các API hiện tại không đáp ứng UI. Không đưa toàn bộ sản phẩm/dịch vụ về FE để tự chia nhóm và phân trang.
- Không triển khai kéo-thả sắp xếp/chuyển nhóm trong đợt này; dùng form sửa hiện có.
- Không mở rộng sang SystemAdmin hoặc Flutter trong đợt này.

### Bước chuẩn bị — Bắt buộc phân nhóm trước khi gộp UI

Theo yêu cầu bổ sung ngày 2026-10-14:

- Tạo/sửa sản phẩm bắt buộc chọn danh mục tồn tại, gồm danh mục cha hoặc con hợp lệ.
- Tạo/sửa dịch vụ bắt buộc chọn loại dịch vụ tồn tại; dịch vụ cha vẫn tùy chọn và không thay thế loại dịch vụ.
- FE đánh dấu bắt buộc, bỏ lựa chọn không phân nhóm và báo lỗi ngay tại trường. BE kiểm tra DTO và service, không cho API trực tiếp bỏ qua ràng buộc.
- Áp dụng cả các form tạo nhanh được tái sử dụng khi làm hợp đồng. Nếu chưa có nhóm, mở quản lý nhóm ở tab mới và tải lại options, giữ nội dung form đang nhập.
- Không thay đổi schema nullable của cột, không thêm FK, không tự gán nhóm mặc định hoặc backfill dữ liệu. Các API xem, khóa/mở, xóa/khôi phục giữ phạm vi nghiệp vụ hiện tại.
- Trước khi chuyển UI mới thành màn chính, rà dữ liệu đang thiếu nhóm. Nếu còn, người dùng phải chọn nhóm qua form sửa; không tự sửa database để làm sạch. Nhóm chưa phân loại trong thiết kế chỉ giúp nhìn thấy mặt hàng chưa phân nhóm, không phải lựa chọn hợp lệ khi tạo/sửa mới.

## 2. Kết quả rà soát thực tế

| Phần | Quan hệ đang có | Điểm cần lưu ý |
| --- | --- | --- |
| Danh mục sản phẩm | `TblCategory.CategoryParentId` → danh mục cha | Có cấu trúc cây; một danh mục có thể vừa có sản phẩm trực tiếp vừa có danh mục con. |
| Sản phẩm | `TblProduct.CategoryId` → danh mục | Mỗi sản phẩm thuộc danh mục trực tiếp của nó; có thể chưa phân danh mục. |
| Loại dịch vụ | `TblServiceType` | Không có cột cha; danh mục dịch vụ hiện tại là danh sách phẳng. |
| Dịch vụ | `TblService.ServiceTypeId` và `ServiceParentId` | Loại dịch vụ và dịch vụ cha là hai quan hệ khác nhau, không được đồng nhất. |

Các màn hình nguồn:

- `Frontend/app/(protected)/catalog/categories/page.tsx`.
- `Frontend/app/(protected)/catalog/products/page.tsx`.
- `Frontend/app/(protected)/catalog/service-types/page.tsx`.
- `Frontend/app/(protected)/catalog/services/page.tsx`.
- Form tương ứng nằm cạnh từng trang; API client nằm trong `Frontend/services/catalog/`.
- Sidebar: `Frontend/components/sidebar.tsx`.
- Component cây hiện có: `Frontend/components/ui/custom/data-tree-table.tsx`; cần đánh giá trước khi dùng vì phân trang cây và phân trang mặt hàng không phải một loại phân trang.

Thiếu sót đã xác định:

1. `CategoryService.GetParentsAsync` hiện chỉ gắn danh mục con trực tiếp vào `Items`. Không thể giả định API này trả đầy đủ cây nhiều cấp.
2. `ValidateParentCategoryAsync` hiện kiểm tra cha tồn tại; cập nhật chặn chọn chính nó nhưng chưa kiểm tra đầy đủ việc chọn hậu duệ làm cha.
3. `ProductFilterRequest.CategoryId` lọc đúng danh mục trực tiếp. Không có lọc riêng nhóm chưa phân danh mục.
4. `ServiceFilterRequest.ServiceTypeId` lọc đúng loại dịch vụ. Không có lọc riêng nhóm chưa phân loại.
5. `ProductStatus`/`ServiceStatus` gồm hoạt động, ngừng hoạt động và đã xóa. Nhãn tổng đếm của các API danh mục hiện tại không đảm bảo cùng phạm vi trạng thái với bảng đang mở; không dùng mù quáng làm số lượng đang hiển thị.
6. Danh mục sản phẩm hiện có thể xóa cả cây con khi cây không chứa sản phẩm. Kiểm tra sử dụng vẫn tính các sản phẩm đã xóa mềm. UI mới phải giải thích phạm vi xóa, không suy ra “xóa được” chỉ từ badge sản phẩm hiện hành bằng 0.
7. API/form dịch vụ đã có options tối giản cho loại dịch vụ và dịch vụ cha. Không thay chúng bằng tải toàn bộ danh sách dịch vụ.

## 3. Thiết kế UI đề xuất

### 3.1. Một trang, hai tab

Đề xuất dùng `/catalog/products` làm URL chính của trang gộp, nhãn **Sản phẩm và dịch vụ**. Đây là chủ ý giữ URL sản phẩm hiện tại, tránh thay đổi trang mặc định `/catalog/products` đã được người dùng lưu trong cấu hình cá nhân.

- `/catalog/products?tab=products`: danh mục và sản phẩm.
- `/catalog/products?tab=services`: loại dịch vụ và dịch vụ.
- Sidebar chỉ còn một mục **Sản phẩm và dịch vụ**.
- Các URL danh mục/dịch vụ/loại dịch vụ hiện tại chuyển hướng tới tab phù hợp của trang chính, không giữ bốn bản UI riêng biệt. Đây chỉ là điều hướng URL, không phải fallback hoặc converter dữ liệu.
- Rà mọi liên kết nội bộ và tùy chọn trang sau đăng nhập để đổi nhãn, không ép ghi lại tùy chọn đã lưu của tài khoản.

Thanh công cụ theo tab: tìm kiếm, trạng thái, khoảng ngày, **Thêm danh mục/loại dịch vụ**, **Thêm sản phẩm/dịch vụ**, **Thu gọn tất cả**. Nhãn và bộ lọc ngày phải nói rõ chúng áp dụng cho mặt hàng, không tự suy ra ngày tạo danh mục.

### 3.2. Tab Sản phẩm: cây danh mục và bảng sản phẩm trực tiếp

```text
Sản phẩm và dịch vụ
[Sản phẩm] [Dịch vụ]

▾ Phần mềm                 2 trực tiếp · 7 toàn nhánh     [+ sản phẩm] [⋯]
    Sản phẩm trực tiếp tại Phần mềm
    Mã       Tên                          Giá     Trạng thái   Thao tác
    SP-01    Bộ công cụ nền tảng           ...     ...          [⋯]
    SP-02    Công cụ tích hợp              ...     ...          [⋯]

    ▸ Quản lý doanh nghiệp  3 trực tiếp · 5 toàn nhánh     [+] [⋯]
    ▸ Công cụ triển khai    0 trực tiếp                   [+] [⋯]

▸ Thiết bị                  4 trực tiếp                   [+] [⋯]
▸ Chưa phân danh mục        2 sản phẩm                    [+] [⋯]
```

Quy tắc bắt buộc:

1. Mở danh mục sẽ hiện **sản phẩm trực tiếp** và các dòng danh mục con; mở tiếp danh mục con để xem sản phẩm của nó.
2. Không cộng sản phẩm con vào bảng sản phẩm cha. Một sản phẩm chỉ xuất hiện một lần ở vị trí được xác định bởi `CategoryId`.
3. Phân biệt rõ badge **trực tiếp** và **toàn nhánh**. Ví dụ cha có 2 sản phẩm, con có 5 thì cha ghi `2 trực tiếp · 7 toàn nhánh`; bảng của cha chỉ có 2 dòng.
4. Cây hiển thị theo quan hệ thực, không hardcode chỉ hai cấp. Cấp sâu vẫn đọc được bằng thụt lề có giới hạn và breadcrumb; không tăng thụt lề vô hạn làm tràn màn hình.
5. Danh mục không có sản phẩm nhưng có con vẫn mở được. Danh mục hoàn toàn trống hiện hướng dẫn thêm sản phẩm hoặc danh mục con.
6. Mở nhiều nhánh được phép; không dùng accordion chỉ cho mở một danh mục toàn trang.
7. Menu danh mục: sửa, thêm danh mục con, thêm sản phẩm, xóa theo quyền và quy tắc hiện có. Không đặt checkbox danh mục cạnh checkbox sản phẩm theo cách khiến người dùng nhầm rằng chọn cha là chọn mọi sản phẩm con.
8. Nhóm **Chưa phân danh mục** chỉ lấy `CategoryId == null`, không phải tất cả sản phẩm. Không chuyển dữ liệu có quan hệ sai vào nhóm này.

### 3.3. Tab Dịch vụ: loại dịch vụ mở xuống danh sách dịch vụ

```text
▾ Triển khai                8 dịch vụ                     [+ dịch vụ] [⋯]
    Tên           Giá dịch vụ   Phí thiết lập   Phí duy trì   Dịch vụ cha   [⋯]
    Triển khai A  ...           ...            ...           Không có
    Hỗ trợ A      ...           ...            ...           Triển khai A
    [Phân trang riêng của nhóm]

▸ Bảo trì                   5 dịch vụ                     [+] [⋯]
▸ Chưa phân loại            1 dịch vụ                     [+] [⋯]
```

- Loại dịch vụ mở xuống các dịch vụ có `ServiceTypeId` tương ứng.
- Không tự thêm cấu trúc loại dịch vụ cha/con vào database.
- `ServiceParentId` giữ nguyên ý nghĩa nghiệp vụ. Bản đầu tiên hiển thị bằng cột/nhãn **Dịch vụ cha**, không dựng thêm một cây dịch vụ bên trong từng loại.
- Nếu cha và con thuộc hai loại khác nhau, mỗi dịch vụ vẫn xuất hiện trong loại của chính nó. Không ép đổi loại, không nhân đôi, không bỏ dịch vụ con vì cha không có trong nhóm đang xem.
- Nhóm **Chưa phân loại** lấy `ServiceTypeId == null`; dịch vụ có cha vẫn có thể nằm ở đây nếu chưa chọn loại.
- Khôi phục dịch vụ con vẫn tuân thủ yêu cầu khôi phục cha trước của backend hiện tại.

### 3.4. Tìm kiếm, bộ lọc và phân trang

- Mặc định hiển thị cây/nhóm. Khi tìm toàn bộ bằng mã/tên mặt hàng, dùng kết quả phẳng có phân trang trong cùng tab, kèm breadcrumb danh mục để không bỏ sót sản phẩm ở nhánh chưa mở.
- Khu vực kết quả danh mục/loại dịch vụ và khu vực kết quả mặt hàng phải phân biệt rõ. Không chỉ tìm trong dữ liệu của những nhánh FE đã tải.
- Bấm danh mục trong kết quả sẽ mở đúng chuỗi tổ tiên, đưa người dùng về chế độ xem nhóm; hành động này không ngầm chuyển mặt hàng sang nhóm khác.
- Nếu triển khai thêm “định vị đúng dòng mặt hàng”, phải chờ đúng trang dữ liệu được tải và DOM sẵn sàng; không dùng timeout cố định để focus. Không hứa định vị dòng khi chỉ mở được nhóm và trang đầu.
- Khi bỏ tìm kiếm, khôi phục trạng thái mở cây trước đó. Trạng thái và khoảng ngày áp dụng nhất quán lên bảng, số lượng và kết quả tìm kiếm.
- Mỗi nhóm mặt hàng mở có phân trang riêng; không trộn danh mục con và mặt hàng vào cùng một mảng rồi phân trang bằng chỉ số dòng cây.
- Không tự tải hết các trang mặt hàng. Không có thao tác “mở tất cả và tải toàn bộ” ở bản đầu tiên.
- Theo dõi trạng thái, lỗi tải và nút thử lại riêng từng nhóm; một nhóm lỗi không làm biến mất các nhóm đã mở khác.
- Mobile giữ cùng mô hình cây, dùng card cho mặt hàng và breadcrumb cho cấp sâu; không tạo luồng khác với desktop.
- Chevron là nút có `aria-expanded`; Enter/Space mở được. Bấm menu/thao tác/checkbox không kích hoạt mở hoặc đóng nhóm.

## 4. Luồng người dùng và bảo toàn nghiệp vụ

1. **Thêm danh mục cha:** bấm Thêm danh mục → form hiện có → lưu → cây hiện danh mục mới; các nhánh khác giữ nguyên.
2. **Thêm danh mục con:** menu trên danh mục → form chọn sẵn cha → lưu → mở chuỗi tổ tiên để thấy danh mục mới. Không chọn chính nó/hậu duệ làm cha khi sửa.
3. **Thêm sản phẩm trong nhóm:** chọn sẵn danh mục trực tiếp của nút đã bấm; người dùng vẫn được đổi danh mục trong form. Khi lưu, cập nhật nhóm đích thực tế, không nhóm ban đầu nếu họ đã đổi.
4. **Thêm dịch vụ trong nhóm:** chọn sẵn loại dịch vụ; không tự chọn dịch vụ cha chỉ vì người dùng mở một loại dịch vụ.
5. **Sửa/chuyển nhóm:** mở form hiện có → lưu → làm mới nhóm cũ, nhóm mới và số lượng tổ tiên bị ảnh hưởng. Không đóng toàn cây, không tự mở danh mục 1.
6. **Khóa/mở:** gọi API hiện có, làm mới đúng nhóm. Picker hợp đồng tiếp tục chỉ cho chọn mặt hàng hoạt động theo quy tắc đang áp dụng.
7. **Xóa mặt hàng:** giữ xóa mềm và dữ liệu hợp đồng đã tham chiếu; dòng biến mất khỏi chế độ hiện hành và có trong chế độ Đã xóa.
8. **Khôi phục:** ở chế độ Đã xóa, xem và xác nhận khôi phục; sau đó là ngừng hoạt động như hiện tại, không tự bật kinh doanh.
9. **Xóa danh mục có con:** xác nhận rõ tên danh mục và số danh mục con sẽ bị xóa. Backend quyết định được xóa hay không, kể cả khi chỉ còn sản phẩm đã xóa mềm; không tự cascade mặt hàng hoặc chuyển sang nhóm chưa phân loại.
10. **Xóa loại dịch vụ:** giữ chặn khi còn dịch vụ tham chiếu; không tự xóa dịch vụ hoặc đổi loại để vượt chặn.
11. **Thao tác hàng loạt:** chỉ tác động những mặt hàng được chọn rõ ràng trong nhóm hiện tại. Đổi nhóm/trang/trạng thái thì xóa selection; không chọn những dòng chưa tải. Xử lý thành công một phần giống luồng hiện tại.
12. Tài khoản có `catalogRead` chỉ xem; `catalogManage` mới thấy thao tác thay đổi. API kiểm tra quyền độc lập với UI và giữ tenant isolation.

## 5. Dữ liệu và API đọc phục vụ trang mới

### 5.1. Metadata nhóm tối giản

Đề xuất endpoint đọc mới, tên cuối cùng xác nhận ở Phase 1:

- `GET /api/catalog/categories/tree`: trả danh sách node tối giản gồm ID, tên, cha, thứ tự, số sản phẩm trực tiếp và toàn nhánh theo bộ lọc trạng thái/ngày.
- `GET /api/catalog/service-types/groups`: trả ID, tên và số dịch vụ theo cùng phạm vi lọc.

Với schema hiện tại, ID danh mục và loại dịch vụ là `byte`, nên có thể tải đầy đủ metadata nhóm nhỏ để dựng cây chính xác. Không đồng nghĩa với tải toàn bộ mặt hàng; không giả lập lấy hết bằng `pageSize=100000`. Nếu sau này tăng phạm vi ID/số nhóm, đánh giá API cây phân trang riêng trong một thay đổi khác.

- Dùng SQL projection/aggregate theo nhóm, không truy vấn sản phẩm riêng cho từng node và không tải nội dung mô tả dài chỉ để đếm.
- `directCount` dùng đúng danh mục; `subtreeCount` tính từ tập node hậu duệ không trùng lặp.
- Đếm mặt hàng hiện hành và đã xóa theo bộ lọc được chọn. Nếu có badge lưu trữ bổ sung thì đặt tên rõ; không dùng tổng mọi trạng thái làm tổng đang xem.
- Dựng cây bằng parent map, có kiểm tra vòng lặp và thiếu cha. Nếu dữ liệu quan hệ không hợp lệ, báo lỗi có thể xử lý, không âm thầm chuyển node về gốc, sửa dữ liệu hoặc che đi.
- `tree` là response đọc; không thêm bảng/cột JSON để lưu cây hay tổng đếm.

### 5.2. Danh sách mặt hàng

- Tái sử dụng API phân trang hiện có với `categoryId`/`serviceTypeId` để lấy mặt hàng trực tiếp.
- Bổ sung `unassignedOnly` mặc định `false` để đọc nhóm chưa phân danh mục/loại. Khi `true`, lọc cột quan hệ bằng `null`; không đồng thời nhận ID nhóm cụ thể. Request mâu thuẫn trả lỗi rõ ràng.
- Không đổi ý nghĩa filter cũ thành “gồm toàn bộ nhánh con”; tránh làm đổi danh sách ở picker/màn hình khác.
- Nếu payload danh sách hiện tại quá lớn, thêm endpoint list-summary với tên/mã/giá/trạng thái/ngày/nhóm cần hiển thị. Form xem/sửa lấy chi tiết bằng `getById`, không coi DTO tối giản là dữ liệu đầy đủ để lưu lại.
- API tìm kiếm toàn cục trả kết quả phân trang và đường dẫn danh mục; không tìm bằng cách tải toàn bộ nhóm rồi gọi mọi trang của từng nhóm.
- Dùng thứ tự ổn định có ID làm tiêu chí cuối, đếm trước phân trang; nhóm không bị mất dòng khi đổi trang.
- Giữ các API CRUD và options đang được contract/báo giá/form khác dùng; không xóa endpoint chỉ vì bốn màn quản lý cũ được gộp.

## 6. Kiến trúc FE và trạng thái

Tách khỏi các file page lớn trước khi gộp, không copy toàn bộ bốn page vào một file:

- `CatalogWorkspace`: tab, toolbar và điều phối modal.
- `ProductCategoryTree`: node danh mục và tổ tiên, không sở hữu toàn bộ dữ liệu sản phẩm.
- `ServiceTypeGroups`: nhóm loại dịch vụ, không giả lập quan hệ cha của loại dịch vụ.
- `CatalogGroupPanel`: phần mở/đóng, trạng thái tải/lỗi/empty dùng chung.
- `ProductGroupList` và `ServiceGroupList`: cột và thao tác riêng từng nghiệp vụ, dùng table/card hiện có.
- `CatalogSearchResults`: kết quả toàn cục và breadcrumb.
- Tái sử dụng `ProductFormModal`, `ServiceFormModal`, `CategoryFormModal`, `ServiceTypeFormModal`; thêm props chọn sẵn nhóm và callback trả nhóm cũ/mới khi cần, không đổi quyền của form.

Quy tắc state:

- Key node có loại và ID, ví dụ `category:12`, `service-type:12`; không dùng index hoặc ID trần dùng chung giữa hai tab.
- Giữ `expandedIds`, pagination/filter/cache theo tab và theo nhóm. Đổi tab rồi quay lại không mất nhánh đã mở.
- Cache key chứa tab, nhóm, trang, pageSize, trạng thái, ngày, keyword và tenant/account. Không giữ cache dùng chung giữa tenant hoặc sau logout.
- Dùng AbortController/request identity chống phản hồi cũ ghi đè bộ lọc mới; hủy khi unmount/đổi phạm vi. Giới hạn request đồng thời khi mở nhiều nhóm.
- Sau mutation, invalidate nhóm và metadata bị ảnh hưởng; giữ expanded state. Nếu trang cuối hết dòng, lùi tới trang hợp lệ.
- Khi form lỗi, giữ dữ liệu đang nhập và vị trí cây. Trạng thái mở cây không nằm trong payload sửa danh mục/mặt hàng.

## 7. Các phase và theo dõi tiến độ

| Phase | Công việc | Trạng thái | Điều kiện hoàn tất |
| --- | --- | --- | --- |
| Chuẩn bị | Bắt buộc danh mục sản phẩm/loại dịch vụ khi tạo/sửa | Đã triển khai, xác minh tự động đạt | 36 test catalog đạt; FE typecheck/lint/build đạt. Chưa UAT trình duyệt; không phải triển khai UI gộp. |
| 0 | Rà soát cấu trúc và chốt thiết kế | Đã rà soát, đã viết plan | Các điểm khác nhau giữa cây danh mục và loại/dịch vụ cha đã được ghi rõ. Chưa viết code triển khai. |
| 1 | Chốt contract API và tách component tái sử dụng | Chưa bắt đầu | Ghi DTO/query, các liên kết cần đổi; tách modal/table mà không làm đổi hành vi hiện tại. |
| 2 | API cây/nhóm, đếm và lọc chưa phân nhóm | Chưa bắt đầu | Test đọc đa cấp, phân trang, trạng thái, tenant/quyền; không đổi schema/FK. |
| 3 | UI tab Sản phẩm | Chưa bắt đầu | Danh mục nhiều cấp, sản phẩm trực tiếp, nhóm chưa phân loại, lazy load và CRUD dùng được. |
| 4 | UI tab Dịch vụ | Chưa bắt đầu | Nhóm loại dịch vụ và dịch vụ cha được xử lý tách biệt; CRUD/xóa/khôi phục không đổi nghiệp vụ. |
| 5 | Tìm kiếm, sidebar, URL và hoàn thiện trạng thái | Chưa bắt đầu | Một mục sidebar; route cũ mở tab đúng; tìm toàn cục, trạng thái mở/pagination và mobile đạt. |
| 6 | Kiểm thử hồi quy và bàn giao | Chưa bắt đầu | Ma trận bên dưới đạt, ghi rõ test tự động/UAT nào đã chạy hoặc còn thiếu. |

### Phase 1 — Chuẩn bị component và hợp đồng API

- [ ] Lập checklist chức năng hiện tại của cả bốn màn, cả xem chi tiết/bulk/khôi phục.
- [ ] Chốt URL chính và tham số tab/nhóm; rà sidebar, landing-page options và liên kết nội bộ.
- [ ] Tách modal/table khỏi route page để các route chuyển hướng không làm mất component đang được contract dùng.
- [ ] Chốt response metadata, phạm vi count, null-group và tìm kiếm; không dùng `items` lẫn cho sản phẩm và danh mục con.
- [ ] Ghi rõ `ServiceParentId` không kế thừa loại dịch vụ và không quyết định vị trí nhóm.

### Phase 2 — API đọc và bảo vệ cây

- [ ] Triển khai API metadata và aggregate, cancellation và kiểm tra quyền.
- [ ] Thêm lọc chưa phân nhóm mà giữ nguyên default query của client hiện có.
- [ ] API cây thể hiện đủ các cấp; có lỗi ổn định nếu vòng lặp/cha không tồn tại, không sửa dữ liệu tự động.
- [ ] Bổ sung kiểm tra tổ tiên khi đổi cha để không tạo vòng lặp; vẫn giữ các lựa chọn cha hợp lệ hiện có, không áp giới hạn hai cấp mới.
- [ ] Test count trực tiếp/toàn nhánh, count theo trạng thái và tránh N+1.

### Phase 3 — Sản phẩm

- [ ] Dựng cây bằng ID ổn định; mở cha có cả danh mục con và sản phẩm trực tiếp.
- [ ] Tải sản phẩm khi mở nhóm; phân trang, lỗi tải và thử lại riêng từng nhóm.
- [ ] CRUD nhóm/mặt hàng, chọn sẵn danh mục, invalidate nhánh cũ/mới.
- [ ] Giữ khóa/mở, xóa mềm, khôi phục và xác nhận xóa cả cây danh mục.

### Phase 4 — Dịch vụ

- [ ] Dựng nhóm loại dịch vụ và nhóm chưa phân loại.
- [ ] Hiển thị dịch vụ cha bằng tên; dịch vụ con vẫn hiện khi cha nằm ở nhóm khác.
- [ ] Chọn sẵn loại khi tạo, giữ picker cha hiện có và restore-parent rule.
- [ ] Giá dịch vụ/phí thiết lập/phí duy trì và thao tác hàng loạt không mất sau gộp.

### Phase 5 — Điều hướng và trải nghiệm hoàn chỉnh

- [ ] Tìm toàn bộ có phân trang và breadcrumb; mở nhóm kết quả chính xác.
- [ ] Trạng thái mở không reset sau lưu, đổi tab, đổi trang con hoặc tải lại dữ liệu nhóm.
- [ ] Đổi sidebar và nhãn tùy chọn landing page; chuyển hướng ba route không còn là trang quản lý độc lập.
- [ ] Duy trì query filter hợp lệ khi điều hướng; không thêm lớp schema legacy.
- [ ] Mobile, keyboard, loading/empty/error và chống response cũ đạt.

### Phase 6 — Kiểm thử và kết thúc

- [ ] Backend test các API đọc/filter/quyền và kiểm tra cây.
- [ ] FE typecheck/lint/build; test state mở, request race và phân trang.
- [ ] UAT theo ma trận dưới; chưa UAT thì đánh dấu rõ, không ghi hoàn tất toàn phase.
- [ ] Hồi quy tạo/sửa hợp đồng, báo giá và khôi phục catalog.
- [ ] Chạy `graphify update .` sau khi sửa code; không cần cập nhật đồ thị chỉ vì viết plan.

## 8. Ma trận luồng cần test và kết quả mong đợi

| STT | Luồng test | Kết quả đúng mong đợi |
| --- | --- | --- |
| 1 | Cha có sản phẩm trực tiếp và danh mục con | Hiện cả hai; sản phẩm của con không lặp trong bảng cha. |
| 2 | Cây ít nhất ba cấp và nhiều nhánh cùng mở | Đủ mọi cấp, không khóa chỉ hai cấp, không mất nhánh khác. |
| 3 | Cha không có sản phẩm, con có sản phẩm | Cha mở được; badge trực tiếp 0 và toàn nhánh đúng. |
| 4 | Danh mục trống | Empty state có nút thêm theo quyền, không lỗi hoặc vô hiệu hóa chevron sai. |
| 5 | Mặt hàng chưa phân nhóm | Chỉ hiện trong nhóm null tương ứng, không lẫn toàn bộ catalog. |
| 6 | Mở nhóm có nhiều trang | Chỉ lấy trang cần xem; page/pageSize độc lập giữa hai nhóm. |
| 7 | Lưu sửa ở một nhóm | Giữ nhánh đang mở và scroll hợp lý; không tự mở nhóm đầu tiên. |
| 8 | Thêm mặt hàng rồi đổi nhóm trong form | Dữ liệu và count nằm ở nhóm đã chọn cuối cùng; nhóm ban đầu không tăng sai. |
| 9 | Chuyển sản phẩm từ danh mục con sang danh mục khác | Nhánh cũ/mới và các badge tổ tiên cập nhật, không nhân đôi dòng. |
| 10 | Đổi cha danh mục hợp lệ | Cây di chuyển đúng, sản phẩm vẫn thuộc danh mục cũ theo ID, không mất mặt hàng. |
| 11 | Chọn chính nó/hậu duệ làm cha | UI ngăn lựa chọn; gọi API trực tiếp cũng bị chặn, không tạo vòng lặp. |
| 12 | Dịch vụ cha/con cùng loại | Cả hai đều có trong nhóm; cột cha hiện tên đúng, không phụ thuộc cha có ở trang hiện tại. |
| 13 | Dịch vụ cha/con khác loại hoặc con chưa phân loại | Mỗi dịch vụ xuất hiện theo loại của chính nó; không bị che hoặc ép đổi loại. |
| 14 | Tìm sản phẩm ở nhánh chưa từng mở | Có trong kết quả toàn cục, breadcrumb đúng, mở nhóm đúng chuỗi tổ tiên. |
| 15 | Tìm danh mục cấp sâu | Tìm thấy bằng tên và đường dẫn; không chỉ tìm cha/con cấp đầu. |
| 16 | Lọc trạng thái/ngày | Bảng, kết quả và badge có cùng phạm vi; tên bộ lọc không gây hiểu nhầm cho danh mục. |
| 17 | Xóa mềm mặt hàng đã có hợp đồng | Hợp đồng/snapshot không đổi; mặt hàng vào chế độ Đã xóa và còn khôi phục được. |
| 18 | Khôi phục sản phẩm/dịch vụ | Chuyển sang ngừng hoạt động; không tự bật; dịch vụ con tuân thủ khôi phục cha trước. |
| 19 | Xóa cha có danh mục con | Hộp xác nhận mô tả cây bị ảnh hưởng; có sản phẩm, kể cả đã xóa mềm, thì BE chặn đúng. |
| 20 | Xóa loại dịch vụ đang được sử dụng | BE chặn; không cascade hoặc gỡ loại khỏi dịch vụ. |
| 21 | Đổi nhóm/trạng thái trong khi đang chọn bulk | Selection bị xóa, không tác động các dòng không còn trong phạm vi hiện tại. |
| 22 | API một nhóm lỗi/mạng chậm, đổi filter liên tục | Nhóm khác giữ dữ liệu; request cũ không ghi đè query mới; retry chỉ tải nhóm lỗi. |
| 23 | Read-only và manage | Read-only không có thao tác thay đổi; gọi API trái quyền/khác tenant bị từ chối. |
| 24 | Đổi tab rồi quay lại, bỏ tìm kiếm | Khôi phục expanded state và pagination; không xóa nội dung form đang mở vì refresh nền. |
| 25 | Truy cập các URL cũ và trang mặc định sản phẩm đã lưu | Đi đúng trang gộp/tab; không 404, không đổi default cá nhân sang hợp đồng ngoài ý muốn. |
| 26 | Mobile và keyboard | Mở/đóng, menu và phân trang dùng được; cây sâu không tràn ngang vô hạn. |
| 27 | Tạo/sửa hợp đồng và báo giá | Picker/quick-create còn dùng được, chỉ mặt hàng được phép mới chọn được; payload giá/thuế không đổi. |

## 9. Nhật ký tiếp tục sau gián đoạn

Mỗi lần tiếp tục phải đọc bảng trạng thái, `git status`, diff và nhật ký trước khi làm. Không coi việc có file component/API là đã hoàn tất phase.

Sau mỗi phase ghi:

```text
Ngày/giờ:
Phase và bước đã làm:
File đã sửa:
Quyết định API/UI mới, nếu có:
Lệnh test + kết quả thực tế:
UAT đã làm/chưa làm:
Lỗi hoặc giới hạn còn lại:
Bước tiếp theo chính xác:
```

Nhật ký ban đầu:

- 2026-10-14: Đã rà bốn màn hình, API/filter, quan hệ danh mục con, dịch vụ cha và quyền/sidebar. Đã viết plan này.
- Chưa thay đổi UI/API/DB cho tính năng gộp; Phase 1–6 chưa bắt đầu.
- 2026-10-14: Bổ sung ràng buộc phân nhóm ở bốn DTO create/update, hai service và hai modal sản phẩm/dịch vụ; thêm test. Không chạy migration hoặc cập nhật dữ liệu thật. Kết quả xác minh sẽ ghi sau khi chạy xong.
- Xác minh bước chuẩn bị: `dotnet test` lọc `CatalogGroupRequirementTests`, `CatalogDeletionTests`, `CatalogRestorationTests`, `CatalogOptionsTests`: 36 đạt, 0 lỗi. `npm run typecheck`, lint các file sửa và `npm run build`: đạt. Test bao gồm thiếu/null/0/ngoài phạm vi, nhóm không tồn tại, tạo/sửa nhóm hợp lệ, xóa/khôi phục/options giữ nguyên nghiệp vụ. Chưa UAT trình duyệt. Bước tiếp theo của UI gộp vẫn là Phase 1.
- Trước khi triển khai, workspace có thay đổi khác về mã khách hàng và trang mặc định; giữ nguyên, không reset hoặc gộp chúng vào kết quả của kế hoạch này.
