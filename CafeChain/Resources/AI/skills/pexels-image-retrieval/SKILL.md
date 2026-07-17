---
name: pexels-image-retrieval
description: Tìm và xếp hạng ảnh tham chiếu Pexels cho sản phẩm CafeChain bằng truy vấn tiếng Anh và metadata. Dùng trong bước chọn ảnh sau khi đã có Visual Specification hợp lệ.
---

# Pexels Reference Retrieval

## Quy trình

1. Tạo ít nhất ba English queries từ subject, container, garnish, style và composition.
2. Gửi orientation và giới hạn kết quả theo cấu hình ứng dụng.
3. Loại `PhotoId` đã dùng hoặc nằm trong excluded list.
4. Loại ảnh dưới độ phân giải tối thiểu, sai orientation hoặc metadata chứa forbidden keywords.
5. Xếp hạng theo metadata relevance và trả attribution.

Không tìm video, collection hoặc media ngoài photo search. Không tải URL tùy ý ngoài URL do Pexels API trả về.

## Giới hạn xác minh

Điểm phù hợp chỉ dựa trên query, alt text, photographer metadata, kích thước và orientation. Luôn yêu cầu người dùng xác nhận ảnh; không tuyên bố đã phân tích nội dung bằng vision model.
