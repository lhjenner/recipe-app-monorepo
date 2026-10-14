import PageTitle from './PageTitle';

interface StubPageProps {
  pageTitle: string;
}

function StubPage({ pageTitle }: StubPageProps) {
  return (
    <>
      <PageTitle title={pageTitle} />
      <main className="app-main">
        <section className="app-page" aria-labelledby="page-heading">
          <h1 id="page-heading">{pageTitle}</h1>
          <p>Coming soon</p>
        </section>
      </main>
    </>
  );
}

export default StubPage;
