// Shapes returned by the Web API's /users endpoints (camelCase JSON).
export interface UserRow {
  id: number;
  name: string;
  phoneNumber: string | null;
}

export interface UserPage {
  items: UserRow[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}
