# NGHIỆP VỤ AI GỢI Ý, TÌM ẢNH PEXELS VÀ TẠO ẢNH BẰNG COMFYUI

Bạn hãy đóng vai một Senior Software Architect có kinh nghiệm về ASP.NET MVC, Layered Architecture, AI Image Generation, Pexels API và ComfyUI.

Hãy phân tích hệ thống hiện tại và triển khai nghiệp vụ AI gợi ý kết hợp Pexels và ComfyUI theo đúng các yêu cầu dưới đây.

---

# 1. Mục tiêu

Xây dựng một quy trình AI hỗ trợ người dùng tạo dữ liệu và hình ảnh cho các form có trường ảnh.

Quy trình bắt buộc phải theo thứ tự:

```text
Người dùng bấm "AI gợi ý"
        ↓
AI sinh từ 1 đến 3 gợi ý
        ↓
Người dùng chọn một gợi ý
        ↓
Hệ thống phân tích nội dung hình ảnh của gợi ý
        ↓
Hệ thống tìm ảnh tham chiếu phù hợp trên Pexels
        ↓
Hệ thống kiểm tra độ phù hợp của ảnh Pexels
        ↓
ComfyUI sử dụng ảnh Pexels làm ảnh tham chiếu
        ↓
ComfyUI tạo ảnh cuối cùng đúng với gợi ý
        ↓
Hệ thống kiểm tra ảnh được tạo
        ↓
Điền dữ liệu và ảnh vào form
```

Không được lấy ngẫu nhiên kết quả đầu tiên từ Pexels.

Không được sử dụng ảnh Pexels khác xa với nội dung gợi ý để đưa vào ComfyUI.

Không được để ComfyUI tạo ảnh chỉ dựa trên tên ngắn hoặc một câu mô tả chung chung.

---

# 2. Phạm vi áp dụng

Nghiệp vụ này phải được thiết kế dùng chung cho tất cả form có trường hình ảnh, ví dụ:

* Drink.
* Drink Category.
* Topping.
* Ingredient.
* Product.
* Combo.
* Promotion.
* Store.
* Supplier.
* Các form khác có trường Image, ImageUrl, Thumbnail hoặc Avatar.

Không được viết cứng toàn bộ logic chỉ dành cho `Create Drink`.

Mỗi loại form có thể có cấu hình hình ảnh riêng nhưng phải sử dụng chung một pipeline:

```text
AI Suggestion
→ Visual Specification
→ Pexels Search
→ Pexels Validation
→ ComfyUI Generation
→ Generated Image Validation
→ Apply To Form
```

---

# 3. Nguyên tắc xử lý chung

## 3.1. Khi người dùng bấm AI gợi ý

Khi bấm nút `AI gợi ý`, hệ thống phải:

1. Đọc loại form hiện tại.
2. Đọc dữ liệu người dùng đã nhập, nếu có.
3. Không yêu cầu người dùng phải chọn sẵn Category, Size, Topping hoặc dữ liệu liên quan.
4. Tự động sinh từ 1 đến 3 gợi ý khác nhau.
5. Không tự động áp dụng gợi ý đầu tiên.
6. Hiển thị các gợi ý để người dùng lựa chọn.
7. Chỉ bắt đầu tìm ảnh sau khi người dùng chọn hoặc áp dụng một gợi ý.

Mỗi gợi ý phải khác nhau rõ ràng về ít nhất một trong các yếu tố:

* Tên.
* Thành phần.
* Hương vị.
* Màu sắc.
* Phong cách hình ảnh.
* Cách trình bày.
* Bối cảnh.
* Đối tượng chính.

---

# 4. Dữ liệu bắt buộc của một gợi ý AI

AI không được chỉ trả về tên và mô tả.

Mỗi gợi ý phải có dữ liệu nghiệp vụ và một bản mô tả hình ảnh có cấu trúc.

Cấu trúc gợi ý đề xuất:

```json
{
  "suggestionId": "uuid",
  "entityType": "Drink",
  "name": "Trà đào cam sả",
  "code": "TRA_DAO_CAM_SA",
  "description": "Trà đào kết hợp cam vàng và sả tươi",
  "businessData": {
    "categoryName": "Trà trái cây",
    "price": 45000,
    "active": true
  },
  "visualSpecification": {
    "primarySubject": "a clear glass of peach orange lemongrass iced tea",
    "subjectType": "beverage",
    "mainIngredients": [
      "peach slices",
      "orange slices",
      "lemongrass",
      "ice cubes",
      "amber tea"
    ],
    "secondaryObjects": [
      "small wooden tray",
      "fresh peach",
      "orange"
    ],
    "excludedObjects": [
      "coffee",
      "milk tea",
      "beer",
      "wine",
      "cake",
      "people",
      "hands",
      "text",
      "logo"
    ],
    "dominantColors": [
      "amber",
      "orange",
      "light yellow"
    ],
    "background": "clean cafe table with soft neutral background",
    "composition": "single centered beverage, product photography",
    "cameraAngle": "three-quarter front view",
    "lighting": "soft natural commercial lighting",
    "imageStyle": "realistic professional food photography",
    "orientation": "square",
    "pexelsQueries": [],
    "comfyPositivePrompt": "",
    "comfyNegativePrompt": ""
  }
}
```

Tên các field có thể điều chỉnh theo cấu trúc dự án hiện tại, nhưng phải giữ được đầy đủ ý nghĩa nghiệp vụ.

---

# 5. Chuẩn hóa nội dung hình ảnh

Trước khi tìm ảnh Pexels, hệ thống phải chuyển gợi ý thành một `Visual Specification`.

`Visual Specification` phải xác định rõ:

* Đối tượng chính cần xuất hiện.
* Loại đối tượng.
* Thành phần nhận diện.
* Màu sắc chủ đạo.
* Bối cảnh.
* Phong cách.
* Góc chụp.
* Bố cục.
* Hướng ảnh.
* Những đối tượng không được xuất hiện.

Ví dụ, không được tìm ảnh chỉ bằng:

```text
Trà đào
```

Phải chuyển thành nội dung cụ thể hơn:

```text
iced peach tea in clear glass with peach slices, orange slices and lemongrass
```

Các truy vấn gửi tới Pexels nên ưu tiên tiếng Anh vì dữ liệu tìm kiếm ảnh bằng tiếng Anh thường chính xác hơn.

Tên tiếng Việt vẫn phải được giữ lại để hiển thị và lưu dữ liệu nghiệp vụ.

---

# 6. Tạo truy vấn tìm kiếm Pexels

## 6.1. Mỗi gợi ý phải có nhiều truy vấn

Hệ thống phải tạo từ 3 đến 6 truy vấn Pexels cho mỗi gợi ý.

Không sử dụng một truy vấn duy nhất.

Các truy vấn phải được sắp xếp từ cụ thể đến tổng quát.

Ví dụ với “Trà đào cam sả”:

```text
1. iced peach orange lemongrass tea in clear glass
2. peach citrus iced tea product photography
3. orange peach tea with ice and fruit slices
4. amber fruit tea cafe drink
5. iced fruit tea in transparent glass
```

Ví dụ với topping trân châu đen:

```text
1. black tapioca pearls in small bowl
2. cooked black boba pearls food photography
3. tapioca pearl topping close up
4. bubble tea black pearls ingredient
```

Ví dụ với nguyên liệu dâu tây:

```text
1. fresh ripe strawberries isolated food photography
2. fresh strawberries in small wooden bowl
3. red strawberry ingredient close up
```

## 6.2. Quy tắc tạo truy vấn

Truy vấn nên được tạo theo cấu trúc:

```text
[đối tượng chính]
+ [thành phần nhận diện]
+ [cách trình bày]
+ [phong cách hình ảnh]
```

Ví dụ:

```text
matcha latte
+ clear glass
+ ice cubes and matcha foam
+ professional product photography
```

Không thêm quá nhiều từ không liên quan vì có thể làm giảm độ chính xác của Pexels.

Không đưa vào truy vấn:

* Giá bán.
* Mã sản phẩm.
* Trạng thái Active.
* Nội dung phân quyền.
* Dữ liệu không liên quan đến hình ảnh.
* Câu mô tả marketing dài.

---

# 7. Quy trình tìm ảnh Pexels

## 7.1. Không lấy kết quả đầu tiên

Đối với mỗi truy vấn, hệ thống phải lấy một tập ứng viên, ví dụ từ 10 đến 20 ảnh.

Sau đó:

1. Gộp kết quả từ các truy vấn.
2. Loại bỏ ảnh trùng.
3. Loại bỏ ảnh có kích thước quá thấp.
4. Loại bỏ ảnh không đúng orientation.
5. Loại bỏ ảnh không phù hợp với đối tượng chính.
6. Chấm điểm các ảnh còn lại.
7. Chọn ảnh có điểm cao nhất.

Không được thực hiện:

```text
Gọi Pexels API
→ Lấy photos[0]
→ Sử dụng ngay
```

## 7.2. Số vòng tìm kiếm

Cho phép tìm kiếm tối đa 3 vòng:

### Vòng 1: Truy vấn chính xác

Sử dụng đối tượng chính và đầy đủ các thành phần nhận diện.

Ví dụ:

```text
iced peach orange lemongrass tea
```

### Vòng 2: Truy vấn rút gọn

Giữ đối tượng chính và hai đặc điểm quan trọng nhất.

Ví dụ:

```text
peach citrus iced tea
```

### Vòng 3: Truy vấn fallback

Giữ đúng loại sản phẩm và phong cách hình ảnh.

Ví dụ:

```text
amber fruit tea product photography
```

Không được fallback sang một nhóm đối tượng khác.

Ví dụ:

* Không tìm thấy trà đào không có nghĩa là được chọn ảnh cà phê.
* Không tìm thấy trân châu đen không có nghĩa là được chọn ảnh chocolate.
* Không tìm thấy dâu tây không có nghĩa là được chọn ảnh cherry.
* Không tìm thấy cửa hàng cà phê không có nghĩa là được chọn nhà hàng sang trọng.

---

# 8. Chấm điểm ảnh Pexels

Mỗi ảnh ứng viên phải được chấm điểm trước khi lựa chọn.

Có thể sử dụng metadata, alt text, caption, từ khóa hoặc mô hình đánh giá hình ảnh nếu hệ thống có hỗ trợ.

Công thức tham khảo:

```text
Tổng điểm =
    35% độ khớp đối tượng chính
  + 20% độ khớp thành phần nhận diện
  + 15% độ khớp loại sản phẩm
  + 10% độ khớp màu sắc
  + 10% độ khớp bố cục và orientation
  + 10% chất lượng và độ phân giải
```

Thang điểm chuẩn hóa từ `0` đến `1`.

## 8.1. Điều kiện chấp nhận

```text
Điểm >= 0.75:
    Có thể tự động chọn làm ảnh tham chiếu.

Điểm từ 0.60 đến dưới 0.75:
    Không tự động chọn ngay.
    Phải thử truy vấn khác hoặc đưa ra các ảnh tốt nhất để người dùng chọn.

Điểm dưới 0.60:
    Loại bỏ.
```

Các ngưỡng có thể được cấu hình, không viết cứng rải rác trong nhiều service.

## 8.2. Điều kiện loại bỏ ngay

Một ảnh phải bị loại ngay nếu:

* Sai hoàn toàn loại đối tượng.
* Không chứa đối tượng chính.
* Chứa đối tượng nằm trong `excludedObjects` ở vị trí nổi bật.
* Là ảnh có người khi yêu cầu ảnh sản phẩm không có người.
* Chứa logo hoặc chữ lớn khi yêu cầu ảnh sạch.
* Là ảnh minh họa hoặc hoạt hình khi yêu cầu ảnh chân thực.
* Có tỷ lệ ảnh không phù hợp nghiêm trọng.
* Ảnh mờ hoặc độ phân giải quá thấp.
* Có nhiều sản phẩm gây nhầm lẫn trong khi yêu cầu một sản phẩm chính.
* Là ảnh cà phê nhưng gợi ý là trà trái cây.
* Là ảnh đồ uống có cồn nhưng gợi ý là đồ uống trong quán cà phê.
* Là ảnh món ăn nhưng gợi ý là topping hoặc nguyên liệu.

---

# 9. Bảo vệ hệ thống khỏi việc lấy sai ảnh

Hệ thống phải có các lớp bảo vệ sau:

## Lớp 1: Chuẩn hóa gợi ý

Tách tên sản phẩm khỏi đối tượng hình ảnh thật sự.

Ví dụ:

```text
Tên: Hoàng hôn nhiệt đới
```

Không được tìm:

```text
tropical sunset
```

Nếu đây là tên của một đồ uống, phải tìm:

```text
orange passion fruit iced drink in clear glass
```

AI phải hiểu ngữ cảnh của entity, không tìm kiếm theo nghĩa đen của tên marketing.

## Lớp 2: Xác định loại đối tượng

Mỗi gợi ý phải có `subjectType`, ví dụ:

```text
beverage
food ingredient
topping
store interior
product package
category banner
```

Ảnh Pexels phải đúng loại đối tượng này.

## Lớp 3: Danh sách từ khóa bắt buộc

Có thể xác định các từ khóa bắt buộc:

```json
{
  "requiredKeywords": [
    "tea",
    "peach",
    "glass"
  ]
}
```

Ảnh ứng viên thiếu phần lớn từ khóa quan trọng sẽ bị giảm điểm hoặc loại bỏ.

## Lớp 4: Danh sách từ khóa loại trừ

Ví dụ:

```json
{
  "forbiddenKeywords": [
    "coffee",
    "beer",
    "wine",
    "cake",
    "person"
  ]
}
```

## Lớp 5: Kiểm tra confidence

Không sử dụng ảnh khi độ tin cậy thấp.

Khi không có ảnh đạt ngưỡng, hệ thống phải trả về trạng thái rõ ràng:

```text
Không tìm thấy ảnh Pexels đủ phù hợp với gợi ý.
```

Không được âm thầm chọn một ảnh sai chỉ để hoàn thành quy trình.

---

# 10. Xử lý khi Pexels không tìm thấy ảnh phù hợp

Nếu sau tối đa 3 vòng vẫn không có ảnh đạt ngưỡng:

1. Không sử dụng ảnh có điểm thấp.
2. Hiển thị tối đa 3 ứng viên gần nhất để người dùng lựa chọn, nếu có.
3. Cho phép người dùng:

   * Chọn một ảnh tham chiếu.
   * Tìm lại bằng từ khóa khác.
   * Yêu cầu AI viết lại mô tả hình ảnh.
   * Tạo bằng ComfyUI mà không dùng ảnh Pexels, nếu hệ thống hỗ trợ chế độ này.
4. Ghi lại nguyên nhân thất bại.
5. Không thay đổi các dữ liệu khác trên form.

Pexels phải được gọi trước theo đúng quy trình, nhưng không bắt buộc phải sử dụng một ảnh Pexels sai khi không có kết quả phù hợp.

---

# 11. Sử dụng ảnh Pexels trong ComfyUI

Ảnh Pexels được chọn phải là ảnh tham chiếu, không phải nội dung cuối cùng bắt buộc phải giữ nguyên.

ComfyUI phải kết hợp:

```text
Ảnh Pexels đã được xác thực
+
Visual Specification
+
Positive Prompt
+
Negative Prompt
```

Tùy workflow ComfyUI hiện tại, có thể sử dụng:

* Image-to-Image.
* IPAdapter.
* ControlNet.
* Reference-only workflow.
* Kết hợp IPAdapter và Image-to-Image.

Ưu tiên IPAdapter hoặc cơ chế reference image nếu workflow hiện tại hỗ trợ, vì mục tiêu là giữ:

* Loại đối tượng.
* Bố cục.
* Góc chụp.
* Cách trình bày.

Nhưng vẫn cho phép thay đổi:

* Thành phần chi tiết.
* Màu sắc.
* Trang trí.
* Background.
* Phong cách ánh sáng.
* Nhận diện riêng của sản phẩm.

Không để ComfyUI sao chép nguyên trạng ảnh Pexels.

---

# 12. Xây dựng Positive Prompt cho ComfyUI

Positive Prompt phải được tạo từ `Visual Specification`, không được chỉ sử dụng tên sản phẩm.

Cấu trúc đề xuất:

```text
[đối tượng chính],
[thành phần quan trọng],
[màu sắc],
[vật chứa hoặc hình dạng],
[cách trình bày],
[bối cảnh],
[bố cục],
[góc chụp],
[ánh sáng],
[phong cách],
[chất lượng ảnh]
```

Ví dụ:

```text
A realistic professional product photo of a peach orange lemongrass iced tea,
served in one tall clear glass,
amber tea color,
visible peach slices, orange slices, fresh lemongrass and ice cubes,
small wooden cafe tray,
clean neutral cafe background,
single centered beverage,
three-quarter front camera view,
soft natural commercial lighting,
sharp focus,
high detail,
realistic food photography,
no brand
```

Prompt phải mô tả rõ số lượng đối tượng chính.

Ví dụ:

```text
one single glass
one bowl of topping
one ingredient package
one storefront
```

Điều này giúp tránh ComfyUI tạo quá nhiều vật thể.

---

# 13. Xây dựng Negative Prompt cho ComfyUI

Negative Prompt phải gồm hai phần:

## 13.1. Negative Prompt chung

```text
low quality,
low resolution,
blurry,
out of focus,
distorted,
deformed,
duplicate objects,
multiple main products,
cropped product,
cut off,
watermark,
logo,
brand name,
text,
letters,
numbers,
signature,
frame,
collage,
illustration,
cartoon,
anime,
3d render,
unrealistic colors,
oversaturated,
dirty background,
messy composition
```

## 13.2. Negative Prompt theo từng gợi ý

Ví dụ với trà đào cam sả:

```text
coffee,
espresso,
milk coffee,
milk tea,
beer,
wine,
cocktail,
cake,
food plate,
person,
face,
hands,
straw covering the drink,
opaque cup,
plastic branded cup
```

Negative Prompt phải được tạo dựa trên:

* Loại entity.
* Đối tượng chính.
* `excludedObjects`.
* Những đối tượng thường gây nhầm lẫn.

---

# 14. Mức độ ảnh hưởng của ảnh tham chiếu

Không được để ảnh Pexels lấn át hoàn toàn prompt.

Cường độ ảnh tham chiếu phải được cấu hình theo loại entity.

Ví dụ tham khảo:

```text
Drink/Product:
    reference strength từ 0.55 đến 0.75

Ingredient/Topping:
    reference strength từ 0.60 đến 0.80

Category banner:
    reference strength từ 0.35 đến 0.60

Store interior:
    reference strength từ 0.50 đến 0.70
```

Nếu sử dụng Image-to-Image, denoise strength có thể nằm trong khoảng tham khảo:

```text
0.40 đến 0.65
```

Không viết cứng một giá trị cho tất cả trường hợp.

Nếu muốn giữ bố cục ảnh Pexels nhiều hơn thì giảm denoise.

Nếu muốn thay đổi thành phần, màu sắc hoặc sản phẩm nhiều hơn thì tăng denoise.

---

# 15. Tạo nhiều ảnh ComfyUI và chọn ảnh tốt nhất

ComfyUI nên tạo từ 2 đến 4 ảnh cho mỗi lần xử lý.

Sau khi tạo xong, hệ thống phải:

1. Kiểm tra file ảnh hợp lệ.
2. Kiểm tra kích thước ảnh.
3. Kiểm tra ảnh có đúng orientation không.
4. Chấm điểm độ phù hợp với `Visual Specification`.
5. Loại ảnh sai đối tượng.
6. Loại ảnh có chữ, logo hoặc watermark.
7. Chọn ảnh có điểm cao nhất.
8. Có thể hiển thị các ảnh còn lại cho người dùng lựa chọn.

Không tự động lấy output đầu tiên của ComfyUI.

Quy trình đúng:

```text
ComfyUI outputs
→ Validate
→ Score
→ Rank
→ Select best image
```

---

# 16. Chấm điểm ảnh sau khi ComfyUI tạo

Ảnh ComfyUI có thể sử dụng công thức:

```text
Tổng điểm =
    35% đúng đối tượng chính
  + 20% đúng thành phần
  + 15% đúng màu sắc
  + 10% đúng bố cục
  + 10% đúng phong cách
  + 10% chất lượng ảnh
```

Ảnh phải bị loại nếu:

* Sai loại sản phẩm.
* Thiếu đối tượng chính.
* Có nhiều đối tượng chính không mong muốn.
* Có chữ hoặc logo sai.
* Có hình người khi không được yêu cầu.
* Bị biến dạng.
* Màu sắc khác hoàn toàn với gợi ý.
* Các thành phần bị tạo sai nghiêm trọng.
* Ảnh không thể sử dụng làm ảnh sản phẩm.

---

# 17. Áp dụng gợi ý vào form

Khi người dùng bấm `Áp dụng`, hệ thống phải:

1. Điền toàn bộ dữ liệu hợp lệ vào input.
2. Chọn đúng dữ liệu cho combobox.
3. Không chỉ điền tên và mô tả.
4. Điền ảnh cuối cùng được tạo bởi ComfyUI.
5. Không tự động lưu form.
6. Cho phép người dùng chỉnh sửa trước khi lưu.
7. Không ghi đè dữ liệu người dùng đã nhập mà không có cảnh báo, nếu dữ liệu đó khác với gợi ý.

Nếu một dữ liệu combobox chưa tồn tại:

* Không tự ý tạo bản ghi mới.
* Cố gắng map với bản ghi gần đúng.
* Nếu không map được thì để trống và thông báo.
* Không gửi một giá trị text vào field yêu cầu ID.

---

# 18. Trạng thái giao diện

Giao diện phải thể hiện rõ trạng thái của pipeline:

```text
Idle
GeneratingSuggestions
SuggestionsReady
SearchingPexels
ValidatingPexelsImages
PexelsReferenceReady
GeneratingWithComfyUI
ValidatingGeneratedImages
Completed
Failed
```

Thông báo ví dụ:

```text
AI đang tạo gợi ý...
Đang phân tích nội dung hình ảnh...
Đang tìm ảnh tham chiếu phù hợp...
Đang kiểm tra độ phù hợp của ảnh...
Đang tạo ảnh sản phẩm bằng ComfyUI...
Đang kiểm tra ảnh được tạo...
Ảnh đã sẵn sàng.
```

Không hiển thị chung một thông báo như “Đang xử lý” trong toàn bộ quá trình.

---

# 19. Hủy và chống gửi trùng

Trong thời gian xử lý:

* Disable nút `AI gợi ý` hoặc sử dụng request key.
* Không cho gửi cùng một yêu cầu nhiều lần.
* Cho phép người dùng hủy tiến trình nếu hệ thống hiện tại hỗ trợ.
* Khi hủy, không áp dụng kết quả trả về sau đó vào form.
* Mỗi lần sinh gợi ý phải có request ID riêng.
* Mỗi lần tìm Pexels phải liên kết với suggestion ID.
* Mỗi lần gọi ComfyUI phải liên kết với ảnh Pexels đã chọn.

Phải tránh trường hợp:

```text
Người dùng chọn gợi ý B
nhưng request cũ của gợi ý A trả về sau
và ghi đè ảnh của gợi ý B.
```

Trước khi cập nhật giao diện, phải kiểm tra request hiện tại còn hợp lệ.

---

# 20. Cache và tái sử dụng

Có thể cache kết quả Pexels theo:

```text
entityType
+ normalized primarySubject
+ mainIngredients
+ orientation
```

Không cache chỉ theo tên hiển thị.

Ví dụ:

```text
“Hoàng hôn nhiệt đới”
```

không phải cache key tốt.

Cache key phải dựa trên nội dung hình ảnh thực tế:

```text
beverage-orange-passion-fruit-iced-drink-square
```

Cache phải có thời gian hết hạn.

Không cache vĩnh viễn một ảnh Pexels đã bị đánh giá sai.

---

# 21. Log nghiệp vụ

Mỗi lần xử lý cần ghi log các thông tin cần thiết:

```text
RequestId
SuggestionId
EntityType
EntityId nếu có
Tên gợi ý
Visual Specification
Danh sách Pexels query
Số ảnh Pexels tìm được
Ảnh Pexels được chọn
Pexels relevance score
ComfyUI workflow được sử dụng
ComfyUI prompt
ComfyUI negative prompt
Số ảnh ComfyUI tạo ra
Ảnh cuối cùng được chọn
Final relevance score
Thời gian xử lý
Trạng thái
Thông báo lỗi
```

Không log API key.

Không log dữ liệu bí mật.

Không log toàn bộ file ảnh dưới dạng base64.

---

# 22. Xử lý lỗi

## 22.1. AI gợi ý lỗi

* Hiển thị thông báo rõ ràng.
* Không gọi Pexels.
* Không gọi ComfyUI.
* Không làm mất dữ liệu đang nhập.

## 22.2. Pexels API lỗi

* Retry có giới hạn.
* Không retry vô hạn.
* Không chuyển sang một ảnh ngẫu nhiên.
* Cho phép tạo lại hoặc tiếp tục bằng ComfyUI không có ảnh tham chiếu nếu nghiệp vụ cho phép.

## 22.3. Không có ảnh Pexels phù hợp

* Không lấy ảnh sai.
* Thông báo không tìm thấy ảnh đủ phù hợp.
* Cho phép sửa từ khóa hoặc chọn ảnh gần nhất.

## 22.4. ComfyUI lỗi

* Giữ lại gợi ý AI.
* Giữ lại ảnh tham chiếu Pexels.
* Cho phép tạo lại ảnh.
* Không bắt người dùng sinh lại toàn bộ gợi ý.

## 22.5. ComfyUI tạo ảnh sai

* Không tự động áp dụng.
* Thử tạo lại với prompt chặt chẽ hơn.
* Tăng negative prompt.
* Giảm ảnh hưởng của ảnh Pexels nếu ảnh tham chiếu gây sai.
* Cho phép chọn ảnh khác từ Pexels.

---

# 23. Cấu hình theo loại entity

Thiết kế một cấu hình dùng chung cho từng nhóm form.

Ví dụ:

```json
{
  "entityType": "Drink",
  "subjectType": "beverage",
  "defaultOrientation": "square",
  "requiredVisualFields": [
    "container",
    "color",
    "mainIngredients",
    "presentation"
  ],
  "defaultExcludedObjects": [
    "person",
    "hands",
    "logo",
    "text",
    "alcohol"
  ],
  "pexelsResultLimit": 15,
  "minimumPexelsScore": 0.75,
  "comfyOutputCount": 3,
  "minimumGeneratedScore": 0.75
}
```

Ví dụ với Topping:

```json
{
  "entityType": "Topping",
  "subjectType": "food ingredient",
  "defaultOrientation": "square",
  "requiredVisualFields": [
    "ingredientType",
    "texture",
    "color",
    "container"
  ],
  "defaultExcludedObjects": [
    "full beverage",
    "person",
    "hands",
    "logo",
    "text"
  ]
}
```

Logic khác nhau giữa các loại form phải nằm trong configuration hoặc strategy phù hợp.

Không sử dụng một chuỗi `if/else` quá dài trong Controller.

---

# 24. Yêu cầu kiến trúc

Tuân thủ Layered Architecture của dự án.

## Controller

Controller chỉ:

* Nhận request.
* Kiểm tra ModelState.
* Gọi Service.
* Trả kết quả.

Controller không:

* Gọi trực tiếp Pexels.
* Gọi trực tiếp ComfyUI.
* Chấm điểm ảnh.
* Xây dựng prompt phức tạp.
* Truy cập trực tiếp DbContext.

## Service

Service chịu trách nhiệm điều phối nghiệp vụ:

```text
Generate suggestions
→ Build visual specification
→ Search reference images
→ Validate candidates
→ Generate image
→ Validate outputs
→ Return result
```

Nên tách các trách nhiệm rõ ràng, ví dụ:

```text
AI Suggestion Service
Visual Specification Builder
Pexels Image Search Service
Image Relevance Service
ComfyUI Generation Service
Generated Image Validation Service
```

Có thể điều chỉnh tên theo cấu trúc hiện tại.

Không tự ý tạo file hoặc interface trùng với những thành phần dự án đã có.

Trước khi thêm mới, phải kiểm tra project hiện tại có service hoặc client tương ứng hay chưa.

## Repository

Repository chỉ xử lý dữ liệu cần lưu trong database, ví dụ:

* Lưu lịch sử AI generation.
* Lưu metadata ảnh.
* Lưu cache.
* Lưu trạng thái request.

Không đưa HTTP call Pexels hoặc ComfyUI vào Repository.

---

# 25. Kết quả API trả về cho frontend

Kết quả không nên chỉ trả về một URL ảnh.

Cấu trúc tham khảo:

```json
{
  "success": true,
  "requestId": "uuid",
  "suggestionId": "uuid",
  "entityType": "Drink",
  "suggestion": {},
  "pexelsReference": {
    "photoId": "123456",
    "previewUrl": "https://...",
    "sourceUrl": "https://...",
    "photographer": "Photographer name",
    "score": 0.86,
    "matchedQuery": "iced peach orange lemongrass tea"
  },
  "generatedImages": [
    {
      "imageId": "uuid",
      "imageUrl": "/uploads/ai/...",
      "score": 0.91,
      "selected": true
    }
  ],
  "selectedImageUrl": "/uploads/ai/final-image.webp",
  "warnings": []
}
```

Khi thất bại:

```json
{
  "success": false,
  "requestId": "uuid",
  "stage": "PexelsValidation",
  "message": "Không tìm thấy ảnh Pexels đủ phù hợp với gợi ý.",
  "retryable": true,
  "candidates": []
}
```

---

# 26. Bản quyền và nguồn ảnh Pexels

Phải giữ metadata cần thiết của ảnh Pexels:

* Pexels Photo ID.
* URL nguồn.
* Tên photographer.
* Trang ảnh, nếu API cung cấp.
* Thời điểm sử dụng.
* Mục đích sử dụng làm reference.

Không sử dụng URL tạm thời làm URL ảnh cuối cùng của sản phẩm.

Ảnh cuối cùng sau ComfyUI phải được xử lý theo cơ chế lưu trữ ảnh hiện tại của dự án.

Không hardcode đường dẫn lưu ảnh nếu hệ thống đã có image storage service.

---

# 27. Ví dụ luồng hoàn chỉnh

Người dùng mở form tạo Drink và bấm `AI gợi ý`.

AI trả về ba lựa chọn:

```text
1. Trà đào cam sả
2. Trà vải hoa hồng
3. Trà dâu bạc hà
```

Người dùng chọn “Trà đào cam sả”.

Hệ thống tạo Visual Specification:

```text
Đối tượng chính:
Một ly trà trái cây màu hổ phách.

Thành phần:
Đào, cam vàng, sả và đá viên.

Vật chứa:
Một ly thủy tinh cao trong suốt.

Bối cảnh:
Bàn quán cà phê sạch, nền trung tính.

Không được có:
Cà phê, trà sữa, bia, rượu, bánh, người, tay, logo và chữ.
```

Hệ thống tạo các query:

```text
iced peach orange lemongrass tea in clear glass
peach citrus iced tea product photography
amber fruit tea with peach and orange slices
iced fruit tea in transparent glass
```

Pexels trả về 40 ứng viên.

Hệ thống:

```text
Loại ảnh trùng
Loại ảnh sai orientation
Loại ảnh cà phê
Loại ảnh cocktail
Loại ảnh có người
Chấm điểm các ảnh còn lại
```

Ảnh tốt nhất có điểm:

```text
0.87
```

Ảnh này được dùng làm reference cho ComfyUI.

ComfyUI nhận:

```text
Reference image
Positive prompt
Negative prompt
Visual Specification
```

ComfyUI tạo ba ảnh:

```text
Ảnh A: 0.84
Ảnh B: 0.92
Ảnh C: 0.76
```

Hệ thống chọn ảnh B.

Khi người dùng bấm `Áp dụng`:

* Điền tên.
* Điền mã.
* Điền mô tả.
* Chọn Category phù hợp.
* Điền giá được gợi ý.
* Điền URL ảnh cuối cùng.
* Không tự động submit form.

---

# 28. Tiêu chí nghiệm thu

Chức năng được xem là hoàn thành khi đáp ứng đầy đủ:

1. AI tạo được từ 1 đến 3 gợi ý.
2. Gợi ý có dữ liệu form và Visual Specification.
3. Không yêu cầu người dùng chọn trước dữ liệu mới sinh được gợi ý.
4. Pexels nhận nhiều truy vấn tìm kiếm.
5. Không lấy mặc định kết quả đầu tiên từ Pexels.
6. Ảnh Pexels được chấm điểm.
7. Ảnh sai đối tượng bị loại.
8. Không sử dụng ảnh có confidence thấp.
9. ComfyUI sử dụng ảnh Pexels đã được xác thực làm reference.
10. Positive Prompt được tạo từ Visual Specification.
11. Có Negative Prompt chung và theo từng đối tượng.
12. ComfyUI tạo nhiều output.
13. Output ComfyUI được kiểm tra và xếp hạng.
14. Không lấy mặc định output đầu tiên.
15. Người dùng có thể xem và chọn kết quả.
16. Khi áp dụng, dữ liệu được điền đầy đủ vào input và combobox.
17. Không tự động lưu form.
18. Không làm mất dữ liệu khi có lỗi.
19. Không để request cũ ghi đè request mới.
20. Logic dùng chung được cho nhiều loại form có ảnh.
21. Không viết cứng riêng cho Create Drink.
22. Controller không gọi trực tiếp Pexels hoặc ComfyUI.
23. Không sử dụng ảnh sai chỉ để hoàn tất quy trình.
24. Có log theo từng giai đoạn.
25. Có xử lý retry và lỗi rõ ràng.

---

# 29. Yêu cầu cuối cùng khi triển khai

Trước khi chỉnh sửa code, hãy:

1. Phân tích các service AI, Pexels, ComfyUI và Image Storage đang có.
2. Xác định form nào đang sử dụng nút AI gợi ý.
3. Xác định DTO request và response hiện tại.
4. Chỉ ra nguyên nhân khiến Pexels đang lấy sai ảnh.
5. Chỉ ra ComfyUI hiện đang dùng text-to-image, image-to-image, IPAdapter hay workflow nào.
6. Tái sử dụng các thành phần hiện có nếu phù hợp.
7. Không tự ý tạo các file trùng chức năng.
8. Không thay đổi những nghiệp vụ không liên quan.
9. Không viết logic gọi API trong Controller.
10. Không hardcode API key.
11. Đưa các ngưỡng score, số lần retry và số lượng ảnh vào configuration.
12. Sau khi hoàn thành, liệt kê rõ:

    * File đã sửa.
    * Method đã sửa.
    * File mới thực sự cần thêm.
    * Luồng xử lý mới.
    * Cấu hình cần bổ sung.
    * Cách kiểm thử.
    * Các trường hợp fallback.

Mục tiêu quan trọng nhất là:

```text
Pexels phải tìm được ảnh tham chiếu gần đúng nhất với gợi ý.
ComfyUI phải tạo ảnh cuối cùng dựa trên cả gợi ý và ảnh tham chiếu.
Hệ thống tuyệt đối không được lấy một ảnh khác xa với nội dung gợi ý chỉ vì đó là kết quả đầu tiên.
```
