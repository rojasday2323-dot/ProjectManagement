import type { CSSProperties, ReactNode } from 'react';

interface Props {
  children: ReactNode;
  style?: CSSProperties;
  id?: string;
}

export function Card({ children, style, id }: Props) {
  return (
    <div
      id={id}
      style={{
        backgroundColor: '#fff',
        borderRadius: 8,
        border: '1px solid #e5e7eb',
        padding: 20,
        boxShadow: '0 1px 3px rgba(0,0,0,0.06)',
        ...style,
      }}
    >
      {children}
    </div>
  );
}
