import { useEffect } from 'react';

interface PageTitleProps {
  title: string;
}

function PageTitle({ title }: PageTitleProps) {
  useEffect(() => {
    document.title = `${title} – Recipe App`;
  }, [title]);

  return null;
}

export default PageTitle;
