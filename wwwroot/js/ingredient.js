let isEdit = false;
let unitCache = [];
let conversionCache = [];

$(document).ready(function () {

    preloadUnits();

    $("#btnCreate").click(openCreateModal);
    $("#btnFilter").click(applyFilter);
    $("#btnClose, #btnClose2").click(closeModal);
    $("#btnSave").click(saveIngredient);

    $("#btnAddRow").click(e => {
        e.preventDefault();
        addConversionRow();
    });

    $(document).on("click", ".edit-btn", function () {
        openEditModal($(this).data("id"));
    });

    $(document).on("click", ".toggle-btn", function () {
        toggleStatus($(this).data("id"));
    });

    $("#ingredientModal").click(function (e) {
        if ($(e.target).is("#ingredientModal")) closeModal();
    });

    $("form").on("submit", function (e) {
        e.preventDefault();
    });
});


// ===== LOAD UNIT =====
function preloadUnits() {
    return fetch("/Admin/AdminIngredient/GetUnits")
        .then(r => r.json())
        .then(data => {
            unitCache = data || [];
        });
}

function loadUnits(selector, type = null) {

    let html = '<option value="">-- Chọn --</option>';

    unitCache
        .filter(u => !type || u.type === type) // 🔥 cùng loại
        .forEach(u => {
            html += `<option value="${u.id}" data-type="${u.type}">${u.text}</option>`;
        });

    $(selector).html(html);
}


// ===== MODAL =====
function showModal() { $("#ingredientModal").addClass("show"); }
function closeModal() { $("#ingredientModal").removeClass("show"); }


// ===== CREATE =====
function openCreateModal() {
    isEdit = false;
    clearForm();

    preloadUnits().then(() => {
        loadUnits("#baseUnitId");
        $("#modalTitle").text("Thêm");
        showModal();
    });
}


// ===== EDIT =====
function openEditModal(id) {

    isEdit = true;
    clearForm();

    Promise.all([
        fetch(`/Admin/AdminIngredient/GetById?id=${id}`).then(r => r.json()),
        preloadUnits()
    ]).then(([res]) => {

        let d = res.data;

        loadUnits("#baseUnitId");

        $("#ingredientId").val(d.ingredientId);
        $("#code").val(d.code);
        $("#name").val(d.name);
        $("#baseUnitId").val(d.baseUnitId);

        conversionCache = d.conversions || [];
        renderConversions(conversionCache);

        $("#modalTitle").text("Cập nhật");
        showModal();
    });
}


// ===== SAVE =====
function saveIngredient() {

    let data = {
        ingredientId: $("#ingredientId").val() || 0,
        code: $("#code").val(),
        name: $("#name").val(),
        baseUnitId: parseInt($("#baseUnitId").val()),
        conversions: collectConversions()
    };

    let url = isEdit ? "/Update" : "/Create";

    fetch(`/Admin/AdminIngredient${url}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data)
    })
        .then(r => r.json())
        .then(res => {

            toast(res.message || "Có lỗi xảy ra", res.success ? "success" : "error");

            if (!res.success) return;

            if (!isEdit) {
                data.ingredientId = res.data; // 🔥 ID thật
                addRowDOM(data);
            } else {
                updateRowDOM(data);
            }

            closeModal();
        });
}


// ===== UPDATE DOM =====
function addRowDOM(d) {

    let unitName = $("#baseUnitId option:selected").text();

    let row = `
    <tr data-id="${d.ingredientId}">
        <td>${d.code}</td>
        <td>${d.name}</td>
        <td>${unitName}</td>
        <td><span class="badge bg-success">Hoạt động</span></td>
        <td class="text-center">
            <i class="fa fa-edit text-primary action-icon edit-btn" data-id="${d.ingredientId}"></i>
            <i class="fa fa-lock text-warning action-icon toggle-btn" data-id="${d.ingredientId}"></i>
        </td>
    </tr>`;

    $("#ingredientTable").prepend(row);
}

function updateRowDOM(d) {

    let tr = $(`tr[data-id='${d.ingredientId}']`);
    let unitName = $("#baseUnitId option:selected").text();

    tr.find("td:eq(0)").text(d.code);
    tr.find("td:eq(1)").text(d.name);
    tr.find("td:eq(2)").text(unitName);
}


// ===== FILTER =====
function applyFilter() {

    let search = $("#searchBox").val().toLowerCase();
    let status = $("#statusFilter").val();

    $("#ingredientTable tr").each(function () {

        let text = $(this).text().toLowerCase();
        let isActive = $(this).find(".badge").text().includes("Hoạt động");

        let matchSearch = text.includes(search);
        let matchStatus =
            !status ||
            (status === "true" && isActive) ||
            (status === "false" && !isActive);

        $(this).toggle(matchSearch && matchStatus);
    });
}


// ===== TOGGLE =====
function toggleStatus(id) {
    fetch(`/Admin/AdminIngredient/ToggleStatus?id=${id}`, { method: "POST" })
        .then(r => r.json())
        .then(res => {

            toast(res.message || "Có lỗi xảy ra", res.success ? "success" : "error");

            if (!res.success) return;

            let badge = $(`tr[data-id='${id}'] .badge`);

            if (badge.hasClass("bg-success")) {
                badge.removeClass("bg-success").addClass("bg-danger").text("Ngưng");
            } else {
                badge.removeClass("bg-danger").addClass("bg-success").text("Hoạt động");
            }
        });
}


// ===== CONVERSION =====
function addConversionRow(data = null) {

    let row = $(`
    <tr>
        <td><select class="form-control fromUnit"></select></td>
        <td><input type="number" class="form-control fromQty"/></td>
        <td><select class="form-control toUnit"></select></td>
        <td><input type="number" class="form-control toQty"/></td>
        <td><i class="fa fa-trash text-danger"></i></td>
    </tr>`);

    $("#conversionTable").append(row);

    loadUnits(row.find(".fromUnit"));
    loadUnits(row.find(".toUnit"));

    // 🔥 filter cùng loại
    row.on("change", ".fromUnit", function () {
        let type = $(this).find("option:selected").data("type");
        loadUnits(row.find(".toUnit"), type);
    });

    // 🔥 auto convert
    row.on("input change", "input,select", function () {
        autoConvert(row);
    });

    row.find(".fa-trash").click(() => row.remove());

    if (data) {
        row.find(".fromUnit").val(data.fromUnitId);
        row.find(".toUnit").val(data.toUnitId);
        row.find(".fromQty").val(data.fromQuantity);
        row.find(".toQty").val(data.toQuantity);
    }
}


// ===== AUTO CONVERT =====
function autoConvert(row) {

    let from = parseInt(row.find(".fromUnit").val());
    let to = parseInt(row.find(".toUnit").val());
    let fromQty = parseFloat(row.find(".fromQty").val());

    if (!from || !to || !fromQty) return;

    // ưu tiên DB
    let conv = conversionCache.find(x =>
        x.fromUnitId == from && x.toUnitId == to
    );

    if (conv) {
        let ratio = conv.toQuantity / conv.fromQuantity;
        row.find(".toQty").val((fromQty * ratio).toFixed(2));
        return;
    }

    // 🔥 fallback chuẩn: luôn về đơn vị nhỏ nhất
    if (baseConvert[from] && baseConvert[from].to === to) {
        row.find(".toQty").val((fromQty * baseConvert[from].ratio).toFixed(2));
    }
}


// ===== RENDER =====
function renderConversions(list) {
    $("#conversionTable").html("");
    list.forEach(c => addConversionRow(c));
}


// ===== COLLECT =====
function collectConversions() {

    let list = [];

    $("#conversionTable tr").each(function () {

        let row = $(this);

        list.push({
            fromUnitId: parseInt(row.find(".fromUnit").val()),
            fromQuantity: parseFloat(row.find(".fromQty").val()),
            toUnitId: parseInt(row.find(".toUnit").val()),
            toQuantity: parseFloat(row.find(".toQty").val())
        });
    });

    return list;
}


// ===== CLEAR =====
function clearForm() {
    $("#ingredientId").val("");
    $("#code").val("");
    $("#name").val("");
    $("#baseUnitId").html("");
    $("#conversionTable").html("");
    conversionCache = [];
}


// ===== BASE CONVERT =====
const baseConvert = {
    2: { to: 1, ratio: 1000 }, // kg -> g
    4: { to: 3, ratio: 1000 }  // l -> ml
};
