// Generated from src/agent-up.css. Do not edit.
export declare const agentUpTheme: {
  readonly colors: Readonly<Record<string, string>>;
  readonly spacing: Readonly<Record<string, number>>;
  readonly radii: Readonly<Record<string, number>>;
  readonly typography: {
    readonly sizeXs: number;
    readonly sizeSm: number;
    readonly sizeMd: number;
    readonly sizeLg: number;
    readonly sizeXl: number;
    readonly sizeDisplay: number;
    readonly tight: number;
    readonly normal: number;
    readonly relaxed: number;
    readonly regular: number;
    readonly medium: number;
    readonly semibold: number;
    readonly bold: number;
    readonly black: number;
  };
  readonly controls: Readonly<Record<string, number>>;
  readonly fonts: Readonly<Record<string, string>>;
  readonly components: Readonly<Record<string, Readonly<Record<string, string | number>>>>;
};
export declare function auBox(...names: Array<string | false | null | undefined>): {
  backgroundColor?: string;
  borderWidth?: number;
  borderColor?: string;
  borderRadius?: number;
  borderTopWidth?: number;
  borderTopColor?: string;
  borderRightWidth?: number;
  borderRightColor?: string;
  borderBottomWidth?: number;
  borderBottomColor?: string;
  borderLeftWidth?: number;
  borderLeftColor?: string;
  width?: number;
  height?: number;
  minHeight?: number;
  minWidth?: number;
  paddingHorizontal?: number;
  paddingVertical?: number;
  opacity?: number;
};
export declare function auText(...names: Array<string | false | null | undefined>): {
  color?: string;
  fontSize?: number;
  fontWeight?: '400' | '500' | '600' | '700' | '800';
  opacity?: number;
  fontFamily?: string;
  textTransform?: 'none' | 'capitalize' | 'uppercase' | 'lowercase';
  letterSpacing?: number;
};
