import PageTitle from './PageTitle';

function NotFoundPage() {
  return (
    <>
      <PageTitle title="Page not found" />
      <main className="app-main">
        <section className="app-page" aria-labelledby="page-heading">
          <h1 id="page-heading">Page not found</h1>
          <p>The page you requested could not be found.</p>
        </section>
      </main>
    </>
  );
}

export default NotFoundPage;
