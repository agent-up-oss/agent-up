export type DatabaseNames = {
  databases: string[];
};

export type DatabaseTables = {
  tables: string[];
};

export type DatabaseQueryResult = {
  columns: string[];
  rows: string[][];
};

export type DatabaseQueryRequest = {
  database: string;
  sql: string;
};
