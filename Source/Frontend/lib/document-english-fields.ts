export const TENANT_ENGLISH_FIELDS = [
  { key: "legalEntityNameEn", label: "Tên công ty tiếng Anh", maxLength: 500 },
  { key: "addressEn", label: "Địa chỉ tiếng Anh", maxLength: 2000 },
  {
    key: "representativeTitleEn",
    label: "Chức danh đại diện tiếng Anh",
    maxLength: 200,
  },
  { key: "bankNameEn", label: "Tên ngân hàng tiếng Anh", maxLength: 500 },
] as const;

export const CUSTOMER_ENGLISH_FIELDS = [
  { key: "customerCompanyEn", label: "Tên công ty tiếng Anh", maxLength: 500 },
  { key: "customerAddressEn", label: "Địa chỉ tiếng Anh", maxLength: 500 },
  {
    key: "customerBankNameEn",
    label: "Tên ngân hàng tiếng Anh",
    maxLength: 500,
  },
  { key: "customerCityEn", label: "Tỉnh/thành tiếng Anh", maxLength: 100 },
  { key: "customerCountryEn", label: "Quốc gia tiếng Anh", maxLength: 100 },
  {
    key: "customerContactPersonTitleEn",
    label: "Chức danh người liên lạc tiếng Anh",
    maxLength: 200,
  },
  {
    key: "customerRepresentativeTitleEn",
    label: "Chức danh đại diện pháp luật tiếng Anh",
    maxLength: 200,
  },
] as const;
